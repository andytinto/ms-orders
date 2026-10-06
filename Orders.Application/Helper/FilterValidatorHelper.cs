using FluentValidation;
using Orders.Application.Common.Models;

namespace Orders.Application.Helper
{
    public class FilterValidator<T> : AbstractValidator<T>
        where T : IStandardListQuery
    {
        public FilterValidator(string[] allowedColumns, string errorMessage)
        {
            this.AddFilterValidation(
                x => x.FilterColumn,
                x => x.FilterValue,
                allowedColumns,
                errorMessage
            );
        }
    }
}
