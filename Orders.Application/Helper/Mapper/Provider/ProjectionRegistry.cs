using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace Orders.Application.Helpers.Mapper.Provider
{
    public sealed class ProjectionRegistry
    {
        private readonly ConcurrentDictionary<(Type Src, Type Dest), LambdaExpression> _maps = new();

        public void Register<TSrc, TDest>(Expression<Func<TSrc, TDest>> expr)
            => _maps[(typeof(TSrc), typeof(TDest))] = expr;

        internal IReadOnlyDictionary<(Type, Type), LambdaExpression> GetAll() => _maps;
    }
}
