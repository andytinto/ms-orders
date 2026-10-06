namespace Orders.Application.Dtos
{
    public class ConnectionStringsOptions
    {
        public string OrdersConnectionString { get; set; } = string.Empty;
        public string CMSConnectionString { get; set; } = string.Empty;
        public string RedisConnection { get; set; } = string.Empty;
        public string ElasticConnection { get; set; } = string.Empty;
    }

    public class BasicOptions
    {
        public string ServiceName { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
    }

    public class RedisOptions
    {
        public string App { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty;
        public string InstanceName { get; set; } = string.Empty;
        public int MstConfigTtl { get; set; }
    }

    public class ExcludeLog
    {
        public List<string> Path { get; set; } = [];
    }

    public class MaskingOptions
    {
        public List<string> FieldsToMask { get; set; } = new();
    }
}
