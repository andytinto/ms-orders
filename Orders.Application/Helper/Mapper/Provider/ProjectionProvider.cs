using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace Orders.Application.Helpers.Mapper.Provider
{
    public sealed class ProjectionProvider : IProjectionProvider
    {
        private readonly IReadOnlyDictionary<(Type Src, Type Dest), LambdaExpression> _maps;

        internal ProjectionProvider(IReadOnlyDictionary<(Type, Type), LambdaExpression> maps)
        {
            _maps = maps;
        }

        public Expression<Func<TSrc, TDest>> Get<TSrc, TDest>()
        {
            if (_maps.TryGetValue((typeof(TSrc), typeof(TDest)), out var expr))
                return (Expression<Func<TSrc, TDest>>)expr;

            throw new InvalidOperationException(
                $"No projection registered: {typeof(TSrc).Name} -> {typeof(TDest).Name}");
        }
    }
}
