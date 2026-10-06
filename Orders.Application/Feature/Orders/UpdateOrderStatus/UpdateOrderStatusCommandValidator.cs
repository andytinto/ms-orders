using FluentValidation;
using Orders.Domain.Enums;

namespace Orders.Application.Feature.Orders.UpdateOrderStatus
{
    public class UpdateOrderStatusCommandValidator : AbstractValidator<UpdateOrderStatusCommand>
    {
        public UpdateOrderStatusCommandValidator()
        {
            RuleFor(x => x.OrderId)
                .NotEmpty().WithMessage("OrderId is required.");

            RuleFor(x => x.NewStatus)
                .NotEmpty().WithMessage("NewStatus is required.")
                .Must(status => OrderStatus.AllStatuses.Contains(status))
                .WithMessage($"NewStatus must be one of: {string.Join(", ", OrderStatus.AllStatuses)}");
        }
    }
}
