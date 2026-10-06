using MediatR;
using Orders.Application.Feature.Orders.Dtos;

namespace Orders.Application.Feature.Orders.GetOrderById
{
    public class GetOrderByIdQuery : IRequest<OrderDetailResponseDto>
    {
        public Guid OrderId { get; set; }

        public GetOrderByIdQuery() { }

        public GetOrderByIdQuery(Guid orderId)
        {
            OrderId = orderId;
        }
    }
}
