using MediatR;
using Orders.Application.Feature.Orders.Dtos;

namespace Orders.Application.Feature.Orders.CreateOrder
{
    public class CreateOrderCommand : IRequest<OrderDetailResponseDto>
    {
        public string CustomerId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public List<OrderItemRequest> Items { get; set; } = new();
        public string? IdempotencyKey { get; set; }
    }
}
