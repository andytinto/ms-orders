using FluentValidation;
using Orders.Application.Common.Models;
using Orders.Application.Constants;

namespace Orders.Application.Helper
{
    public class SortingValidator<T> : AbstractValidator<T>
        where T : IStandardListQuery
    {
        public SortingValidator(string[] allowedColumns)
        {
            When(x => x.SortBy != null, () =>
            {
                this.AddSortByValidation(x => x.SortBy!.Column, x => x.SortBy!.OrderBy);

                RuleFor(x => x.SortBy!.OrderBy)
                    .MustBeIn(GeneralConstant.AllowedSortDirections, ErrorConstant.ErrorValidationSortingType)
                    .When(x => !string.IsNullOrWhiteSpace(x.SortBy?.OrderBy));

                RuleFor(x => x.SortBy!.Column)
                    .MustBeIn(allowedColumns, ErrorConstant.ErrorNotAllowedSortingColumn.Replace("@columns", string.Join(" or ", allowedColumns)))
                    .When(x => !string.IsNullOrWhiteSpace(x.SortBy?.Column));
            });
        }
    }
}
