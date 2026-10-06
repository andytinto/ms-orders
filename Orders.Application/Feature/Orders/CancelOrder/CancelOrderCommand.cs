using MediatR;
using Orders.Application.Feature.Orders.Dtos;

namespace Orders.Application.Feature.Orders.CancelOrder
{
    public class CancelOrderCommand : IRequest<OrderDetailResponseDto>
    {
        public Guid OrderId { get; set; }
        public string? Reason { get; set; }
        public string? RowVersion { get; set; }
    }
}
