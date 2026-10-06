using Microsoft.Extensions.Options;
using Orders.Application.Dtos;
using Orders.Application.Interfaces;

namespace Orders.Infrastructure.Services
{
    public class AppsettingServices : IAppsettingServices
    {
        private readonly IOptionsMonitor<RedisOptions> _redisOptions;
        private readonly IOptionsMonitor<BasicOptions> _basicOptions;
        private readonly IOptionsMonitor<ConnectionStringsOptions> _connectionStringOptions;
        private readonly IOptionsMonitor<ExcludeLog> _excludeLog;
        private readonly IOptionsMonitor<MaskingOptions> _maskingOption;

        public AppsettingServices(
            IOptionsMonitor<RedisOptions> redisOptions,
            IOptionsMonitor<BasicOptions> basicOptions,
            IOptionsMonitor<ConnectionStringsOptions> connectionStringOptions,
            IOptionsMonitor<ExcludeLog> excludeLog,
            IOptionsMonitor<MaskingOptions> maskingOption)
        {
            _redisOptions = redisOptions;
            _basicOptions = basicOptions;
            _connectionStringOptions = connectionStringOptions;
            _excludeLog = excludeLog;
            _maskingOption = maskingOption;
        }

        public RedisOptions GetRedisOptions()
        {
            return _redisOptions.CurrentValue;
        }

        public BasicOptions GetBasicOptions()
        {
            return _basicOptions.CurrentValue;
        }

        public ConnectionStringsOptions GetConnectionStringsOptions()
        {
            return _connectionStringOptions.CurrentValue;
        }

        public ExcludeLog GetExcludeLog()
        {
            return _excludeLog.CurrentValue;
        }

        public MaskingOptions GetMaskingOptions()
        {
            return _maskingOption.CurrentValue;
        }
    }
}
