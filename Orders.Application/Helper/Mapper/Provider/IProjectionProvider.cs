using System.Linq.Expressions;

namespace Orders.Application.Helpers.Mapper.Provider
{
    public interface IProjectionProvider
    {
        Expression<Func<TSrc, TDest>> Get<TSrc, TDest>();
    }
    public interface IProjectionProfile
    {
        void Register(ProjectionRegistry registry);
    }
}
