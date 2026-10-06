using FluentValidation;
using Orders.Application.Helper;

namespace Orders.Application.Feature.Orders.OrderList
{
    public class OrderListQueryVal : AbstractValidator<OrderListQuery>
    {
        public OrderListQueryVal()
        {
            RuleFor(x => x.Page)
                .MustBePositivePage();

            RuleFor(x => x.PageSize)
                .GreaterThan(0).WithMessage("PageSize must be greater than 0.");
        }
    }
}
