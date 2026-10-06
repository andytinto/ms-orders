using Orders.Application.Dtos;

namespace Orders.Application.Interfaces
{
    public interface IAppsettingServices
    {
        RedisOptions GetRedisOptions();
        BasicOptions GetBasicOptions();
        ConnectionStringsOptions GetConnectionStringsOptions();
        ExcludeLog GetExcludeLog();
        MaskingOptions GetMaskingOptions();
    }
}
