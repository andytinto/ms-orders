using System.Linq.Expressions;
using System.Reflection;

namespace Orders.Application.Helpers.Mapper
{
    internal interface IMapDef
    {
        (Type Src, Type Dest) Key { get; }
        Delegate Compile();
    }
    internal sealed class MapDef<TSrc, TDest> : IMapDef where TDest : new()
    {
        public (Type Src, Type Dest) Key => (typeof(TSrc), typeof(TDest));

        internal bool AutoEnabled { get; set; }
        internal List<(MemberInfo DestMember, LambdaExpression SrcExpr)> Overrides { get; } = new();
        internal List<MemberRule<TSrc>> Rules { get; } = new();

        public Delegate Compile()
        {
            //// Func<object, TDest>
            //var srcObj = Expression.Parameter(typeof(object), "srcObj");

            //var srcVar = Expression.Variable(typeof(TSrc), "src");
            //var assign = Expression.Assign(srcVar, Expression.Convert(srcObj, typeof(TSrc)));

            //var bindings = new List<MemberBinding>();

            //// 1) AUTO: same-name & assignable types
            //if (AutoEnabled)
            //{
            //    var srcProps = typeof(TSrc)
            //        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            //        .Where(p => p.CanRead)
            //        .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

            //    foreach (var dp in typeof(TDest).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite))
            //    {
            //        if (!srcProps.TryGetValue(dp.Name, out var sp)) continue;
            //        if (!dp.PropertyType.IsAssignableFrom(sp.PropertyType)) continue;

            //        var srcValue = Expression.Property(srcVar, sp);
            //        bindings.Add(Expression.Bind(dp, srcValue));
            //    }
            //}

            //// 2) OVERRIDES: replace binding if same dest member already exists
            //foreach (var (destMember, srcExpr) in Overrides)
            //{
            //    bindings.RemoveAll(b => b.Member.Name.Equals(destMember.Name, StringComparison.OrdinalIgnoreCase));

            //    var replacedBody = new ReplaceParameterVisitor(srcExpr.Parameters[0], srcVar)
            //        .Visit(srcExpr.Body)!;

            //    bindings.Add(Expression.Bind(destMember, replacedBody));
            //}

            //var init = Expression.MemberInit(Expression.New(typeof(TDest)), bindings);

            //var body = Expression.Block([srcVar], assign, init);

            //return Expression.Lambda<Func<object, TDest>>(body, srcObj).Compile();



            var srcObj = Expression.Parameter(typeof(object), "srcObj");

            var srcVar = Expression.Variable(typeof(TSrc), "src");
            var destVar = Expression.Variable(typeof(TDest), "dest");

            var assignSrc = Expression.Assign(srcVar, Expression.Convert(srcObj, typeof(TSrc)));

            // ===== 1) Base bindings from Auto() =====
            var bindings = new List<MemberBinding>();

            if (AutoEnabled)
            {
                var srcProps = typeof(TSrc)
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead)
                    .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

                foreach (var dp in typeof(TDest)
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite))
                {
                    if (!srcProps.TryGetValue(dp.Name, out var sp)) continue;
                    if (!dp.PropertyType.IsAssignableFrom(sp.PropertyType)) continue;

                    bindings.Add(Expression.Bind(dp, Expression.Property(srcVar, sp)));
                }
            }

            // ===== 2) Apply UNCONDITIONAL overrides into MemberInit =====
            foreach (var rule in Rules.Where(r => r.ConditionExpr is null))
            {
                bindings.RemoveAll(b => 
                    b.Member.Name.Equals(rule.DestMember.Name, StringComparison.OrdinalIgnoreCase));

                var valueBody = new ReplaceParameterVisitor(
                    rule.ValueExpr.Parameters[0], 
                    srcVar
                ).Visit(rule.ValueExpr.Body)!;

                bindings.Add(Expression.Bind(rule.DestMember, valueBody));
            }

            // ===== 3) Create dest with bindings =====
            var createDest = Expression.Assign(
                destVar,
                Expression.MemberInit(Expression.New(typeof(TDest)), bindings)
            );

            // ===== 4) Apply CONDITIONAL overrides as assignments =====
            var statements = new List<Expression>
            {
                assignSrc,
                createDest
            };

            foreach (var rule in Rules.Where(r => r.ConditionExpr is not null))
            {
                var condBody = new ReplaceParameterVisitor(rule.ConditionExpr!.Parameters[0], srcVar)
                    .Visit(rule.ConditionExpr!.Body)!;

                var valueBody = new ReplaceParameterVisitor(rule.ValueExpr.Parameters[0], srcVar)
                    .Visit(rule.ValueExpr.Body)!;

                var setExpr = Expression.Assign(
                    Expression.MakeMemberAccess(destVar, rule.DestMember),
                    valueBody
                );

                statements.Add(Expression.IfThen(condBody, setExpr));
            }

            statements.Add(destVar);

            var body = Expression.Block(
                variables: new[] { srcVar, destVar },
                expressions: statements
            );

            return Expression.Lambda<Func<object, TDest>>(body, srcObj).Compile();
        }
    }

    internal sealed class ReplaceParameterVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly Expression _to;

        public ReplaceParameterVisitor(ParameterExpression from, Expression to)
        {
            _from = from;
            _to = to;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _from ? _to : base.VisitParameter(node);
    }

    internal sealed class MemberRule<TSrc>
    {
        public required MemberInfo DestMember { get; init; }
        public required LambdaExpression ValueExpr { get; init; }
        public LambdaExpression? ConditionExpr { get; init; } // Expression<Func<TSrc,bool>>
    }
}
