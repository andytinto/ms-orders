using MediatR;
using Microsoft.EntityFrameworkCore;
using Orders.Application.Common.Exceptions;
using Orders.Application.Common.Interfaces;
using Orders.Application.Feature.Orders.Dtos;
using Orders.Domain.Models;

namespace Orders.Application.Feature.Orders.GetOrderById
{
    public class GetOrderByIdHandler : IRequestHandler<GetOrderByIdQuery, OrderDetailResponseDto>
    {
        private readonly IOrdersDbContext _dbContext;

        public GetOrderByIdHandler(IOrdersDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<OrderDetailResponseDto> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
        {
            var order = await _dbContext.Orders
                .Include(o => o.OrderDetails)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == request.OrderId && !o.IsDeleted, cancellationToken);

            if (order == null)
            {
                throw new NotFoundException($"Order with ID '{request.OrderId}' was not found.");
            }

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
