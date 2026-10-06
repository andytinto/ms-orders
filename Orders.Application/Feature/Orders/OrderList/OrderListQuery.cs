using MediatR;
using Orders.Application.Common.Models;

namespace Orders.Application.Feature.Orders.OrderList
{
    public class OrderListQuery : IRequest<PagedResponse<OrderItemDto>>, IStandardListQuery
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? FilterColumn { get; set; }
        public string? FilterValue { get; set; }
        public SortingOptions? SortBy { get; set; }

        public string? CustomerId { get; set; }
        public string? Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
