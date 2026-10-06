using MediatR;
using Orders.Application.Feature.Orders.Dtos;

namespace Orders.Application.Feature.Orders.UpdateOrderStatus
{
    public class UpdateOrderStatusCommand : IRequest<OrderDetailResponseDto>
    {
        public Guid OrderId { get; set; }
        public string NewStatus { get; set; } = string.Empty;
        public string? RowVersion { get; set; }
        public string? Notes { get; set; }
    }
}
