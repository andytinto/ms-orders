namespace Orders.Domain.Common
{
    public abstract class BaseEntity
    {
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public string ModifiedBy { get; set; } = string.Empty;
        public DateTime? ModifiedDate { get; set; }
        public bool IsDeleted { get; set; } = false;
        public int RowStatus { get; set; } = 0;
    }
}
