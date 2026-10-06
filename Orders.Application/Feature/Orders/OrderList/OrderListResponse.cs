namespace Orders.Application.Feature.Orders.OrderList
{
    public class OrderItemDto
    {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string StatusCode { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
    }
}
