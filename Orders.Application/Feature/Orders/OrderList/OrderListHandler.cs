using MediatR;
using Microsoft.EntityFrameworkCore;
using Orders.Application.Common.Exceptions;
using Orders.Application.Common.Interfaces;
using Orders.Application.Common.Models;
using Orders.Application.Constants;

namespace Orders.Application.Feature.Orders.OrderList
{
    public class OrderListHandler : IRequestHandler<OrderListQuery, PagedResponse<OrderItemDto>>
    {
        private readonly IOrdersDbContext _dbContext;

        public OrderListHandler(IOrdersDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<PagedResponse<OrderItemDto>> Handle(
            OrderListQuery request,
            CancellationToken cancellationToken)
        {
            var pageSize = request.PageSize > 0 ? request.PageSize : 10;
            var page = request.Page > 0 ? request.Page : 1;

            var query = _dbContext.Orders.AsNoTracking()
                .Where(x => !x.IsDeleted);

            if (!string.IsNullOrWhiteSpace(request.CustomerId))
            {
                query = query.Where(x => x.CustomerId == request.CustomerId.Trim());
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                query = query.Where(x => x.StatusCode == request.Status.Trim());
            }

            if (request.StartDate.HasValue)
            {
                query = query.Where(x => x.OrderDate >= request.StartDate.Value);
            }

            if (request.EndDate.HasValue)
            {
                query = query.Where(x => x.OrderDate <= request.EndDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.FilterValue))
            {
                var filter = request.FilterValue.Trim();
                query = query.Where(x =>
                    x.OrderNumber.Contains(filter) ||
                    x.CustomerName.Contains(filter));
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            if (page > 1 && page > totalPages && totalCount > 0)
                throw new NotFoundException(GeneralConstant.ERROR_MESSAGE_DATA_NOT_FOUND);

            if (request.SortBy != null && !string.IsNullOrWhiteSpace(request.SortBy.Column))
            {
                var isDesc = string.Equals(request.SortBy.OrderBy, "desc", StringComparison.OrdinalIgnoreCase);
                query = request.SortBy.Column.ToLowerInvariant() switch
                {
                    "totalamount" => isDesc ? query.OrderByDescending(x => x.TotalAmount) : query.OrderBy(x => x.TotalAmount),
                    "ordernumber" => isDesc ? query.OrderByDescending(x => x.OrderNumber) : query.OrderBy(x => x.OrderNumber),
                    "customername" => isDesc ? query.OrderByDescending(x => x.CustomerName) : query.OrderBy(x => x.CustomerName),
                    "statuscode" or "status" => isDesc ? query.OrderByDescending(x => x.StatusCode) : query.OrderBy(x => x.StatusCode),
                    _ => isDesc ? query.OrderByDescending(x => x.OrderDate) : query.OrderBy(x => x.OrderDate)
                };
            }
            else
            {
                query = query.OrderByDescending(x => x.OrderDate);
            }

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new OrderItemDto
                {
                    OrderId = x.OrderId,
                    OrderNumber = x.OrderNumber,
                    CustomerId = x.CustomerId,
                    CustomerName = x.CustomerName,
                    OrderDate = x.OrderDate,
                    TotalAmount = x.TotalAmount,
                    StatusCode = x.StatusCode,
                    PaymentStatus = x.PaymentStatus
                })
                .ToListAsync(cancellationToken);

            return new PagedResponse<OrderItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalCount = totalCount,
                Data = items
            };
        }
    }
}
