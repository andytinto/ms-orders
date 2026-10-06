using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Orders.Domain.Common;

namespace Orders.Domain.Models
{
    [Table("OrderDetails")]
    public class OrderDetail : BaseEntity
    {
        [Key]
        public Guid OrderDetailId { get; set; }
        public Guid OrderId { get; set; }
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal SubTotal { get; set; }
        public string Notes { get; set; } = string.Empty;

        [ForeignKey(nameof(OrderId))]
        public virtual Order? Order { get; set; }
    }
}
