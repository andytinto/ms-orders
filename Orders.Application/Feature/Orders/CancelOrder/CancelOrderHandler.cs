using MediatR;
using Microsoft.EntityFrameworkCore;
using Orders.Application.Common.Exceptions;
using Orders.Application.Common.Interfaces;
using Orders.Application.Feature.Orders.Dtos;
using Orders.Domain.Enums;
using Orders.Domain.Models;

namespace Orders.Application.Feature.Orders.CancelOrder
{
    public class CancelOrderHandler : IRequestHandler<CancelOrderCommand, OrderDetailResponseDto>
    {
        private readonly IOrdersDbContext _dbContext;

        public CancelOrderHandler(IOrdersDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<OrderDetailResponseDto> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
        {
            var order = await _dbContext.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.OrderId == request.OrderId && !o.IsDeleted, cancellationToken);

            if (order == null)
            {
                throw new NotFoundException($"Order with ID '{request.OrderId}' was not found.");
            }

            // Scenario B Concurrency check: verify RowVersion if provided
            if (!string.IsNullOrEmpty(request.RowVersion) && order.RowVersion != null && order.RowVersion.Length > 0)
            {
                try
                {
                    var clientRowVersion = Convert.FromBase64String(request.RowVersion);
                    if (!order.RowVersion.SequenceEqual(clientRowVersion))
                    {
                        throw new ConflictException(
                            $"Concurrency conflict: The order has been modified by another administrator. Current status: '{order.StatusCode}'. Please refresh and try again.");
                    }
                }
                catch (FormatException)
                {
                    throw new BadRequestException("Invalid RowVersion format. Must be a valid Base64 string.");
                }
            }

            // Cancellation is only allowed if status is Pending or Confirmed
            if (order.StatusCode != OrderStatus.Pending && order.StatusCode != OrderStatus.Confirmed)
            {
                throw new UnprocessableEntityException(
                    $"Order '{order.OrderNumber}' with status '{order.StatusCode}' cannot be cancelled. Only Pending or Confirmed orders can be cancelled.");
            }

            // Restock items in Products
            var productGuids = order.OrderDetails
                .Select(d => Guid.TryParse(d.ProductId, out var guid) ? guid : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .ToList();

            var products = await _dbContext.Products
                .Where(p => productGuids.Contains(p.Id))
                .ToListAsync(cancellationToken);

            var productMap = products.ToDictionary(p => p.Id);

            foreach (var detail in order.OrderDetails)
            {
                if (Guid.TryParse(detail.ProductId, out var productId) && productMap.TryGetValue(productId, out var product))
                {
                    product.StockQuantity += detail.Quantity;
                }
            }

            order.StatusCode = OrderStatus.Cancelled;
            if (!string.IsNullOrWhiteSpace(request.Reason))
            {
                order.Notes = string.IsNullOrWhiteSpace(order.Notes)
                    ? $"Cancellation reason: {request.Reason}"
                    : $"{order.Notes} | Cancellation reason: {request.Reason}";
            }

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException(
                    "Concurrency conflict: The order was updated concurrently by another transaction. Please refresh and try again.");
            }

            return MapToResponse(order);
        }

        private static OrderDetailResponseDto MapToResponse(Order order)
        {
            return new OrderDetailResponseDto
            {
                OrderId = order.OrderId,
                OrderNumber = order.OrderNumber,
                CustomerId = order.CustomerId,
                CustomerName = order.CustomerName,
                ShippingAddress = order.ShippingAddress,
                OrderDate = order.OrderDate,
                TotalAmount = order.TotalAmount,
                StatusCode = order.StatusCode,
                PaymentStatus = order.PaymentStatus,
                Notes = order.Notes,
                IdempotencyKey = order.IdempotencyKey,
                RowVersion = order.RowVersion != null ? Convert.ToBase64String(order.RowVersion) : string.Empty,
                Items = order.OrderDetails.Select(d => new OrderItemDetailDto
                {
                    OrderDetailId = d.OrderDetailId,
                    ProductId = d.ProductId,
                    ProductName = d.ProductName,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    SubTotal = d.SubTotal
                }).ToList()
            };
        }
    }
}
