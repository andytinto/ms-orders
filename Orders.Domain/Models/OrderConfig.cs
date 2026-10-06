using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Orders.Domain.Common;

namespace Orders.Domain.Models
{
    [Table("OrderConfig")]
    public class OrderConfig : BaseEntity
    {
        [Key]
        public Guid OrderConfigId { get; set; }
        public string ConfigGroup { get; set; } = string.Empty;
        public string ConfigName { get; set; } = string.Empty;
        public string ConfigValue { get; set; } = string.Empty;
        public string Additional { get; set; } = string.Empty;
    }
}
