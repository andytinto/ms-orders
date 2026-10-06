
namespace Orders.Application.Dtos
{
    public class ActivityLogQuery
    {
        public Guid AuditId { get; set; }
        public string Feature { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Activity { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public string ActivityDetails { get; set; } = string.Empty;
    }

    public class PagedResult<T>
    {
        public int TotalHits { get; set; }
        public int TotalPages { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public IEnumerable<T> Documents { get; set; } = [];
    }
}
