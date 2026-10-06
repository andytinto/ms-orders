using FluentValidation.TestHelper;
using Orders.Application.Common.Models;
using Orders.Application.Helper;
using Xunit;

namespace Orders.Test.Helper
{
    public class SortingValidatorTests
    {
        public class TestOrderListQuery : IStandardListQuery
        {
            public int Page { get; set; } = 1;
            public string? FilterColumn { get; set; }
            public string? FilterValue { get; set; }
            public SortingOptions? SortBy { get; set; }
        }

        private readonly SortingValidator<TestOrderListQuery> _validator =
            new SortingValidator<TestOrderListQuery>(new[] { "OrderNumber", "OrderDate", "TotalAmount" });

        [Fact]
        public void Validate_SortByNull_Passes()
        {
            var query = new TestOrderListQuery { SortBy = null };
            var result = _validator.TestValidate(query);
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_ValidSortBy_Passes()
        {
            var query = new TestOrderListQuery { SortBy = new SortingOptions { Column = "OrderNumber", OrderBy = "asc" } };
            var result = _validator.TestValidate(query);
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_InvalidColumn_Fails()
        {
            var query = new TestOrderListQuery { SortBy = new SortingOptions { Column = "Invalid", OrderBy = "asc" } };
            var result = _validator.TestValidate(query);
            result.ShouldHaveValidationErrorFor("SortBy.Column");
        }

        [Fact]
        public void Validate_InvalidOrderBy_Fails()
        {
            var query = new TestOrderListQuery { SortBy = new SortingOptions { Column = "OrderNumber", OrderBy = "Invalid" } };
            var result = _validator.TestValidate(query);
            result.ShouldHaveValidationErrorFor("SortBy.OrderBy");
        }

        [Fact]
        public void Validate_MissingColumn_Fails()
        {
            var query = new TestOrderListQuery { SortBy = new SortingOptions { Column = null, OrderBy = "asc" } };
            var result = _validator.TestValidate(query);
            result.ShouldHaveValidationErrorFor("SortBy.Column");
        }

        [Fact]
        public void Validate_MissingOrderBy_Fails()
        {
            var query = new TestOrderListQuery { SortBy = new SortingOptions { Column = "OrderNumber", OrderBy = null } };
            var result = _validator.TestValidate(query);
            result.ShouldHaveValidationErrorFor("SortBy.OrderBy");
        }
    }
}
