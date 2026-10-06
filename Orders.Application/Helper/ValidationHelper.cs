using System.Linq.Expressions;
using FluentValidation;
using FluentValidation.Results;
using Orders.Application.Common.Exceptions;
using Orders.Application.Constants;

namespace Orders.Application.Helper
{
    public static class ValidationHelper
    {
        public static IRuleBuilderOptions<T, int> MustBePositivePage<T>(
            this IRuleBuilder<T, int> ruleBuilder)
        {
            return ruleBuilder
                .GreaterThan(0).WithMessage(ErrorConstant.ErrorValidationPage);
        }

        public static void ThrowIfHeaderNotFound(string header, bool condition)
        {
            if (condition)
            {
                throw new BadRequestException(GeneralConstant.ERROR_MESSAGE_HEADER_NOT_FOUND);
            }
        }

        public static IRuleBuilderOptions<T, string?> MustBeIn<T>(
            this IRuleBuilder<T, string?> ruleBuilder, IEnumerable<string> allowed, string message)
        {
            return ruleBuilder
                .Must(y => allowed.Any(a => a.Equals(y, StringComparison.OrdinalIgnoreCase)))
                .WithMessage(message);
        }

        public static IRuleBuilderOptionsConditions<T, string?> IsGuidString<T>(
            this IRuleBuilder<T, string?> ruleBuilder, string propertyName)
        {
            return ruleBuilder
                .NotNull().WithMessage(ErrorConstant.ErrorValidationEmptyValue)
                .Must(x => x != "").WithMessage(ErrorConstant.ErrorValidationEmptyString)
                .MinimumLength(36).MaximumLength(36).WithMessage(ErrorConstant.ErrorValidationMaxLength)
                .Custom((id, context) =>
                {
                    if (!Guid.TryParse(id, out _))
                        context.AddFailure(propertyName, ErrorConstant.ErrorValidationMaxLength);
                    else if (Guid.Parse(id) == Guid.Empty)
                        context.AddFailure(propertyName, ErrorConstant.ErrorValidationEmptyValue);
                });
        }

        public static IRuleBuilderOptions<T, string?> MustBeNotNullAndNonEmptyString<T>(
            this IRuleBuilder<T, string?> ruleBuilder)
        {
            return ruleBuilder
                .NotEmpty().NotNull().WithMessage(ErrorConstant.ErrorValidationEmptyValue);
        }

        #region filter validation
        public static void AddFilterValidation<T>(
            this AbstractValidator<T> validator,
            Expression<Func<T, string?>> filterColumnExpr,
            Expression<Func<T, string?>> filterValueExpr,
            string[] allowedColumns,
            string errorMessageTemplate)
        {
            var filterColumnFunc = filterColumnExpr.Compile();
            var filterValueFunc = filterValueExpr.Compile();

            // Validation: FilterColumn must include allowed list
            validator.RuleFor(filterColumnExpr)
                .Must(col => allowedColumns
                    .Any(allowed => allowed.Equals(col, StringComparison.OrdinalIgnoreCase)))
                .WithMessage(errorMessageTemplate.Replace("@columns", string.Join(" or ", allowedColumns)))
                .When(x => !string.IsNullOrWhiteSpace(filterColumnFunc(x)));

            // FilterColumn can't null if FilterValue any
            validator.RuleFor(filterColumnExpr)
                .NotEmpty().WithMessage(ErrorConstant.ErrorValidationFilterColumn)
                .When(x => !string.IsNullOrWhiteSpace(filterValueFunc(x)));

            // FilterValue can't null if FilterColumn any
            validator.RuleFor(filterValueExpr)
                .NotEmpty().WithMessage(ErrorConstant.ErrorValidationFilterValue)
                .When(x => !string.IsNullOrWhiteSpace(filterColumnFunc(x)));
        }
        #endregion filter validation

        #region sorting validation

        public static IRuleBuilderOptions<T, string?> RequiredIfOtherSortFieldPresent<T>(
            this IRuleBuilder<T, string?> ruleBuilder,
            Func<T, string?> otherFieldGetter,
            string errorMessage)
        {
            return ruleBuilder
                .NotEmpty().When(x => !string.IsNullOrWhiteSpace(otherFieldGetter(x)))
                .WithMessage(errorMessage);
        }

        public static void AddSortByValidation<T>(
            this AbstractValidator<T> validator,
            Expression<Func<T, string?>> getColumn,
            Expression<Func<T, string?>> getOrderBy)
        {
            validator.RuleFor(getColumn)
                .RequiredIfOtherSortFieldPresent(getOrderBy.Compile(), ErrorConstant.ErrorValidationSortingColumn);

            validator.RuleFor(getOrderBy)
                .RequiredIfOtherSortFieldPresent(getColumn.Compile(), ErrorConstant.ErrorValidationSortingOrderBy);
        }

        #endregion sorting validation
    }
}
