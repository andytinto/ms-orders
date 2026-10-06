using System.Data;

namespace Orders.Application.Constants
{
    public static class GeneralConstant
    {
        public static class TransactionSettings
        {
            public static readonly IsolationLevel DefaultIsolationLevel = IsolationLevel.ReadCommitted;
        }

        public static readonly string[] AllowedSortDirections = ["asc", "desc"];
        public static readonly string FormatDateOnly = "yyyy-MM-dd";
        public static readonly string FormatDateWithTimeZone = "yyyy-MM-dd HH:mm:ss";

        // default user
        public const string DEFAULT_USER = "System";

        // standar response code base on scenario
        public const string RESPONSE_CODE_00 = "00";
        public const string RESPONSE_CODE_98 = "98";
        public const string RESPONSE_CODE_99 = "99";
        public const string RESPONSE_CODE_99A = "99A";

        // default error message
        public const string ERROR_MESSAGE_BAD_REQUEST_HEADER = "Property header cannot be null";
        public const string ERROR_MESSAGE_DATA_NOT_FOUND = "Data Not Found";
        public const string ERROR_MESSAGE_ALREADY_EXIST = "Data already exists";
        public const string ERROR_MESSAGE_HEADER_NOT_FOUND = "Header cannot be null";
        public const string ERROR_MESSAGE_BAD_REQUEST = "Bad Request";
        public const string ERRPR_MESSAGE_UNAUTHORIZED = "Unauthorized";

        // default success message
        public const string SUCCESS_MESSAGE = "Success";

        public static class ArticleStatus
        {
            public const string Published = "Published";
            public const string Draft = "Draft";
            public const string Unpublished = "Unpublished";
        }

        public static class AllowedFilterColumn
        {
            public static readonly string[] ArticleListFilterColumn = ["Title", "Category", "TotalViews", "ModifiedDate", "Status"];
            public static readonly string[] CategoriesListFilterColumn = ["CategoryName", "Slug", "Description", "TotalArticles"]; 

            public static readonly string[] PortalCategoriesListFilterColumn = ["CategoryName", "Slug", "Description"];
        }


        public static class Header
        {
            public const string TimezoneOffset = "Timezone-Offset";
            public const string ScreenContext = "Screen-Context";
            public const string AcceptLanguage = "Accept-Language";

            public static class HeaderValue
            {
                public const string ScreenContextRevenue = "revenue";
            }
        }
    }
}
