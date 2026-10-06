using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Orders.Domain.Common;

namespace Orders.Domain.Models
{
    [Table("Orders")]
    public class Order : BaseEntity
    {
        [Key]
        public Guid OrderId { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(50)]
        public string OrderNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CustomerId { get; set; } = string.Empty;

        [MaxLength(200)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string ShippingAddress { get; set; } = string.Empty;

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [MaxLength(50)]
        public string StatusCode { get; set; } = "Pending";

        [MaxLength(50)]
        public string PaymentStatus { get; set; } = "Unpaid";

        [MaxLength(100)]
        public string? IdempotencyKey { get; set; }

        public string Notes { get; set; } = string.Empty;

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];

        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    }
}
