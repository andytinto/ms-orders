namespace Orders.Application.Constants
{
    public static class ErrorConstant
    {
        public const string ErrorValidationPage = "Page must be a positive number.";

        // response error message for filtering
        public const string ErrorNotAllowedFilterColumn = "Filtering is only allowed on the following columns: @columns";
        public const string ErrorValidationFilterColumn = "FilterColumn direction is required when FilterValue is provided.";
        public const string ErrorValidationFilterValue = "FilterValue direction is required when FilterColumn is provided.";

        // response error message for sorting
        public const string ErrorValidationSortingType = "Sorting type must be 'asc' or 'desc'";
        public const string ErrorNotAllowedSortingColumn = "Sorting is only allowed on the following columns: @columns";
        public const string ErrorValidationSortingColumn = "Column direction is required when SortBy.OrderBy is provided.";
        public const string ErrorValidationSortingOrderBy = "OrderBy direction is required when SortBy.Column is provided.";

        public const string ErrorValidationEmptyValue = "Request cannot be null or empty string.";
        public const string ErrorValidationEmptyString = "Request cannot be empty string.";
        public const string ErrorValidationMaxLength = "Length request has exceed maximum length.";

        public const string ErrorValidationDuplication = "Duplicate languageCode found in details array";

        public const string ErrorValidationLanguageCode = "Invalid Language Code";
        public const string ErrorCategoryHasLinkedArticles = "Cannot remove category because it still has linked articles";
    }
}
