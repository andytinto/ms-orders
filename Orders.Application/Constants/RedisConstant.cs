
namespace Orders.Application.Constants
{
    public static class RedisConstant
    {
        public const string MST_CONFIG_KEY = "HelpCentre_Config";

        public static class Dropdown
        {
            public const string ConfigGroup = "Dropdown";
            public const string ConfigName = "PageSize";
            public const int DefaultPageSize = 10;

            public const string AdditionalCategories = "Categories";
        }

        public static class PageSize 
        {
            public const string ConfigGroup = "Admin";
            public const string ConfigName = "PageSize";
            public const int DefaultPageSize = 50;

            public const string AdditionalArticles = "Articles";
            public const string AdditionalCategories = "Categories";
            public const string AdditionalAuditTrail = "AuditTrail";
        }

        public static class Search
        {
            public const string ConfigGroup = "Search";

            public const string AdditionalCategories = "Categories";
            public const string AdditionalResult = "Result";
        }

        public static class Admin 
        {
            public const string ConfigGroup = "Admin";

            public const string ConfigNameLanguage = "Accept-Language";

            public const string AdditionalCategories = "Categories";
            public const string AdditionalArticles = "Articles";
        }

        public static class ElasticsearchInfo 
        {
            public const string DefaultIndex = "cms-helpcentre-audit";
            public const string ConfigGroup = "ElasticSearch";

            public const string ConfigNameIndex = "Index";

            public const string AdditionalAuditTrail = "AuditTrail";

            public static class ElasticFields
            {
                public const string DefaultSortField = "createdTime";
                public const string DefaultEntityIdField = "entityId";
                public const string DefaultFeatureField = "feature";
                public const string DefaultAuditId = "auditId";
            }
        }

        public static class AuditTrail
        {
            public static class Feature
            {
                public const string Categories = "HelpCentre Categories";
                public const string Articles = "HelpCentre Articles";
            }

            public static class Action
            {
                public const string Update = "Updated Data";
                public const string Remove = "Removed Data";
                public const string Submit = "Submit Data";
            }

            public static class Activity
            {
                public const string Update = "Updated Data";
                public const string Remove = "Removed Data";
                public const string Submit = "Submit Data";
            }
        }

    }
}
