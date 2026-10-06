using FluentValidation;

namespace Orders.Application.Feature.Orders.CancelOrder
{
    public class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
    {
        public CancelOrderCommandValidator()
        {
            RuleFor(x => x.OrderId)
                .NotEmpty().WithMessage("OrderId is required.");

            RuleFor(x => x.RowVersion)
                .NotEmpty().WithMessage("RowVersion is required for concurrency control.");
        }
    }
}
