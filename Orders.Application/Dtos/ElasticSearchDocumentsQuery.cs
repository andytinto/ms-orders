using Orders.Application.Common.Models;
using Orders.Application.Constants;

namespace Orders.Application.Dtos
{
    public class ElasticSearchDocumentsQuery
    {
        public Dictionary<string, object> SearchParameters { get; set; } = new Dictionary<string, object>();
        public string IndexName { get; set; } = string.Empty;
        public SearchMode SearchMode { get; set; } = SearchMode.Balanced;
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? DateField { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = RedisConstant.PageSize.DefaultPageSize;
        public SortingOptions? SortBy { get; set; }
        public TimeSpan Offset { get; set; }
        public string[] ExactMatchFields { get; set; } =
        {
            RedisConstant.ElasticsearchInfo.ElasticFields.DefaultEntityIdField,
            RedisConstant.ElasticsearchInfo.ElasticFields.DefaultSortField,
            RedisConstant.ElasticsearchInfo.ElasticFields.DefaultAuditId
        };
    }

    public enum SearchMode
    {
        Fast,
        Balanced,
        Deep
    }
}
