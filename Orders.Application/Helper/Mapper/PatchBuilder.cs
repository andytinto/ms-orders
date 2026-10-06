using System.Linq.Expressions;
using System.Reflection;

namespace Orders.Application.Helpers.Mapper
{
    public sealed class PatchBuilder<TSrc, TDest>
    {
        private readonly PatchDef<TSrc, TDest> _def;

        internal PatchBuilder(PatchDef<TSrc, TDest> def) => _def = def;

        public PatchBuilder<TSrc, TDest> ForMemberIf<TMember>(
            Expression<Func<TDest, TMember>> destMember,
            Func<TSrc, TMember> valueFactory,
            Func<TSrc, bool> precondition)
        {
            var member = GetMember(destMember);

            if (member is not PropertyInfo prop || !prop.CanWrite)
                throw new ArgumentException("Destination must be a writable property", nameof(destMember));

            _def.Steps.Add((src, dest) =>
            {
                if (!precondition(src)) return;
                var value = valueFactory(src);
                prop.SetValue(dest, value);
            });


            return this;
        }

        private static MemberInfo GetMember<TMember>(Expression<Func<TDest, TMember>> expr)
            => expr.Body switch
            {
                MemberExpression m => m.Member,
                UnaryExpression { Operand: MemberExpression m } => m.Member,
                _ => throw new ArgumentException("Destination must be a member expression, e.g. d => d.AliasCode")
            };
    }
}
