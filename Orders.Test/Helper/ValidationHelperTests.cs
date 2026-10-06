using System;
using System.Collections.Generic;
using Orders.Application.Helper;
using Orders.Application.Constants;
using Orders.Application.Common.Exceptions;
using FluentValidation;
using FluentValidation.TestHelper;
using Xunit;

namespace Orders.Test.Helper
{
    public class ValidationHelperTests
    {
        public class TestModel
        {
            public int Page { get; set; }
            public string? LanguageCode { get; set; }
            public string? GuidString { get; set; }
            public string? RequiredString { get; set; }
            public string? FilterColumn { get; set; }
            public string? FilterValue { get; set; }
            public string? SortColumn { get; set; }
            public string? SortOrder { get; set; }
        }

        private class TestModelValidator : AbstractValidator<TestModel>
        {
            public TestModelValidator()
            {
                RuleFor(x => x.Page).MustBePositivePage();
                RuleFor(x => x.LanguageCode).MustBeIn(new[] { "en", "id" }, "Invalid language");
                RuleFor(x => x.GuidString).IsGuidString("GuidString");
                RuleFor(x => x.RequiredString).MustBeNotNullAndNonEmptyString();

                this.AddFilterValidation(
                    x => x.FilterColumn,
                    x => x.FilterValue,
                    new[] { "Title", "Category" },
                    "Column @columns only"
                );

                RuleFor(x => x.SortColumn)
                    .RequiredIfOtherSortFieldPresent(x => x.SortOrder, "Sort column required");
            }
        }

        private readonly TestModelValidator _validator = new TestModelValidator();

        [Fact]
        public void MustBePositivePage_Valid_Passes()
        {
            var model = new TestModel { Page = 1 };
            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.Page);
        }

        [Fact]
        public void MustBePositivePage_ZeroOrNegative_Fails()
        {
            var modelNegative = new TestModel { Page = -5 };
            var resultNegative = _validator.TestValidate(modelNegative);
            resultNegative.ShouldHaveValidationErrorFor(x => x.Page);

            var modelZero = new TestModel { Page = 0 };
            var resultZero = _validator.TestValidate(modelZero);
            resultZero.ShouldHaveValidationErrorFor(x => x.Page);
        }

        [Fact]
        public void ThrowIfHeaderNotFound_ConditionTrue_ThrowsBadRequestException()
        {
            Assert.Throws<BadRequestException>(() => ValidationHelper.ThrowIfHeaderNotFound("HeaderName", true));
        }

        [Fact]
        public void ThrowIfHeaderNotFound_ConditionFalse_DoesNotThrow()
        {
            ValidationHelper.ThrowIfHeaderNotFound("HeaderName", false);
        }

        [Fact]
        public void MustBeIn_ValueInAllowed_Passes()
        {
            var model = new TestModel { LanguageCode = "en" };
            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.LanguageCode);
        }

        [Fact]
        public void MustBeIn_ValueNotInAllowed_Fails()
        {
            var model = new TestModel { LanguageCode = "jp" };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.LanguageCode);
        }

        [Fact]
        public void IsGuidString_NullAndEmpty_Fails()
        {
            var modelNull = new TestModel { GuidString = null };
            var resultNull = _validator.TestValidate(modelNull);
            resultNull.ShouldHaveValidationErrorFor(x => x.GuidString);

            var modelEmpty = new TestModel { GuidString = "" };
            var resultEmpty = _validator.TestValidate(modelEmpty);
            resultEmpty.ShouldHaveValidationErrorFor(x => x.GuidString);
        }

        [Fact]
        public void IsGuidString_InvalidGuid_Fails()
        {
            var model = new TestModel { GuidString = "not-a-valid-guid-string-with-36-chars" };
            var result = _validator.TestValidate(model);
            Assert.Contains(result.Errors, e => e.PropertyName == "GuidString");
        }

        [Fact]
        public void IsGuidString_EmptyGuid_Fails()
        {
            var model = new TestModel { GuidString = Guid.Empty.ToString() };
            var result = _validator.TestValidate(model);
            Assert.Contains(result.Errors, e => e.PropertyName == "GuidString");
        }

        [Fact]
        public void IsGuidString_ValidGuid_Passes()
        {
            var model = new TestModel { GuidString = Guid.NewGuid().ToString() };
            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.GuidString);
        }

        [Fact]
        public void MustBeNotNullAndNonEmptyString_Valid_Passes()
        {
            var model = new TestModel { RequiredString = "hello" };
            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.RequiredString);
        }

        [Fact]
        public void MustBeNotNullAndNonEmptyString_NullOrEmpty_Fails()
        {
            var modelNull = new TestModel { RequiredString = null };
            var resultNull = _validator.TestValidate(modelNull);
            resultNull.ShouldHaveValidationErrorFor(x => x.RequiredString);

            var modelEmpty = new TestModel { RequiredString = "" };
            var resultEmpty = _validator.TestValidate(modelEmpty);
            resultEmpty.ShouldHaveValidationErrorFor(x => x.RequiredString);
        }

        [Fact]
        public void AddFilterValidation_ValidColumnAndValue_Passes()
        {
            var model = new TestModel { FilterColumn = "Title", FilterValue = "search" };
            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.FilterColumn);
            result.ShouldNotHaveValidationErrorFor(x => x.FilterValue);
        }

        [Fact]
        public void AddFilterValidation_InvalidColumn_Fails()
        {
            var model = new TestModel { FilterColumn = "InvalidColumn", FilterValue = "search" };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.FilterColumn);
        }

        [Fact]
        public void AddFilterValidation_NullColumnWithValue_Fails()
        {
            var model = new TestModel { FilterColumn = null, FilterValue = "search" };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.FilterColumn);
        }

        [Fact]
        public void AddFilterValidation_ColumnWithNullValue_Fails()
        {
            var model = new TestModel { FilterColumn = "Title", FilterValue = null };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.FilterValue);
        }

        [Fact]
        public void RequiredIfOtherSortFieldPresent_Valid_Passes()
        {
            var model = new TestModel { SortColumn = "Title", SortOrder = "asc" };
            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.SortColumn);
        }

        [Fact]
        public void RequiredIfOtherSortFieldPresent_SortOrderPresentButColumnMissing_Fails()
        {
            var model = new TestModel { SortColumn = null, SortOrder = "asc" };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.SortColumn);
        }
    }
}
