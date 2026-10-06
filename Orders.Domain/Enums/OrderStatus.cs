namespace Orders.Domain.Enums
{
    public static class OrderStatus
    {
        public const string Pending = "Pending";
        public const string Confirmed = "Confirmed";
        public const string Shipped = "Shipped";
        public const string Delivered = "Delivered";
        public const string Cancelled = "Cancelled";

        public static readonly string[] AllStatuses =
        [
            Pending,
            Confirmed,
            Shipped,
            Delivered,
            Cancelled
        ];

        public static bool IsTerminal(string status)
        {
            return status == Delivered || status == Cancelled;
        }

        public static bool CanTransition(string currentStatus, string nextStatus)
        {
            if (currentStatus == nextStatus) return true;

            return currentStatus switch
            {
                Pending => nextStatus is Confirmed or Cancelled,
                Confirmed => nextStatus is Shipped or Cancelled,
                Shipped => nextStatus is Delivered,
                Delivered => false, // Terminal
                Cancelled => false, // Terminal
                _ => false
            };
        }
    }
}
