using MediatR;
using Microsoft.EntityFrameworkCore;
using Orders.Application.Common.Exceptions;
using Orders.Application.Common.Interfaces;
using Orders.Application.Feature.Orders.Dtos;
using Orders.Domain.Enums;
using Orders.Domain.Models;

namespace Orders.Application.Feature.Orders.UpdateOrderStatus
{
    public class UpdateOrderStatusHandler : IRequestHandler<UpdateOrderStatusCommand, OrderDetailResponseDto>
    {
        private readonly IOrdersDbContext _dbContext;

        public UpdateOrderStatusHandler(IOrdersDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<OrderDetailResponseDto> Handle(UpdateOrderStatusCommand request, CancellationToken cancellationToken)
        {
            var order = await _dbContext.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.OrderId == request.OrderId && !o.IsDeleted, cancellationToken);

            if (order == null)
            {
                throw new NotFoundException($"Order with ID '{request.OrderId}' was not found.");
            }

            // Scenario B: Check optimistic concurrency token if provided
            if (!string.IsNullOrEmpty(request.RowVersion) && order.RowVersion != null && order.RowVersion.Length > 0)
            {
                try
                {
                    var clientRowVersion = Convert.FromBase64String(request.RowVersion);
                    if (!order.RowVersion.SequenceEqual(clientRowVersion))
                    {
                        throw new ConflictException(
                            $"Concurrency conflict: The order was modified by another administrator. Current status is '{order.StatusCode}'. Please refresh and try again.");
                    }
                }
                catch (FormatException)
                {
                    throw new BadRequestException("Invalid RowVersion format. Must be a valid Base64 string.");
                }
            }

            // State Machine Transition Validation
            if (order.StatusCode == request.NewStatus)
            {
                return MapToResponse(order);
            }

            if (OrderStatus.IsTerminal(order.StatusCode))
            {
                throw new UnprocessableEntityException(
                    $"Order '{order.OrderNumber}' is in terminal status '{order.StatusCode}' and cannot be modified.");
            }

            if (!OrderStatus.CanTransition(order.StatusCode, request.NewStatus))
            {
                throw new UnprocessableEntityException(
                    $"Invalid status transition from '{order.StatusCode}' to '{request.NewStatus}'.");
            }

            // If transitioning to Cancelled, restock inventory items
            if (request.NewStatus == OrderStatus.Cancelled)
            {
                await RestockOrderItemsAsync(order, cancellationToken);
            }

            order.StatusCode = request.NewStatus;
            if (!string.IsNullOrEmpty(request.Notes))
            {
                order.Notes = request.Notes;
            }

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException(
                    "Concurrency conflict: The order was updated concurrently by another user. Please refresh and try again.");
            }

            return MapToResponse(order);
        }

        private async Task RestockOrderItemsAsync(Order order, CancellationToken cancellationToken)
        {
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
