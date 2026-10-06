using System.Linq.Expressions;
using System.Reflection;

namespace Orders.Application.Helpers.Mapper
{
    /// <summary>
    /// Provides configuration and builder pattern for custom object mapping definitions.
    /// </summary>
    /// <remarks>
    /// File: MapperConfig.cs <br/>
    /// Feature: Object Mapping <br/>
    /// Description: Configuration class that allows defining custom mappings between source and destination types using fluent builder pattern. Supports auto-mapping for same-named properties and manual mapping overrides. Handles both regular mapping and patch mapping for partial updates. <br/>
    /// Usage: Used in dependency injection setup to configure all object mappings. <br/>
    /// Version History: <br/>
    /// - 1.0.0, 2025-05-15, Developer: Initial implementation
    /// </remarks>
    public sealed class MapperConfig
    {
        private readonly List<IMapDef> _defs = new();
        private readonly List<IPatchDef> _patchDefs = new();

        public MapBuilder<TSrc, TDest> CreateMap<TSrc, TDest>() where TDest : new()
        {
            var def = new MapDef<TSrc, TDest>();
            _defs.Add(def);
            return new MapBuilder<TSrc, TDest>(def);
        }

        // precondition
        public PatchBuilder<TSrc, TDest> CreatePatchMap<TSrc, TDest>()
        {
            var def = new PatchDef<TSrc, TDest>();
            _patchDefs.Add(def);
            return new PatchBuilder<TSrc, TDest>(def);
        }

        internal IReadOnlyList<IMapDef> GetDefs() => _defs;
        internal IReadOnlyList<IPatchDef> GetPatchDefs() => _patchDefs;
    }
    public sealed class MapBuilder<TSrc, TDest> where TDest : new()
    {
        private readonly MapDef<TSrc, TDest> _def;

        internal MapBuilder(MapDef<TSrc, TDest> def) => _def = def;

        /// <summary>
        /// Auto-map untuk property dengan nama yang sama (case-insensitive) dan type assignable.
        /// </summary>
        public MapBuilder<TSrc, TDest> Auto()
        {
            _def.AutoEnabled = true;
            return this;
        }

        /// <summary>
        /// Override manual mapping untuk member tertentu (mis. beda nama field).
        /// </summary>
        public MapBuilder<TSrc, TDest> ForMember<TMember>(
            Expression<Func<TDest, TMember>> dest,
            Expression<Func<TSrc, TMember>> src)
        {
            var destMember = GetDestMember(dest);
           
            _def.Rules.Add(new MemberRule<TSrc>
            {
                DestMember = destMember,
                ValueExpr = src,
                ConditionExpr = null
            });

            return this;
        }

        public MapBuilder<TSrc, TDest> ForMemberWithCondition<TMember>(
        Expression<Func<TDest, TMember>> dest,
        Expression<Func<TSrc, TMember>> src,
        Expression<Func<TSrc, bool>> precondition)
        {
            var destMember = GetDestMember(dest);

            _def.Rules.Add(new MemberRule<TSrc>
            {
                DestMember = destMember,
                ValueExpr = src,
                ConditionExpr = precondition
            });

            return this;
        }

        private static MemberInfo GetDestMember<TMember>(Expression<Func<TDest, TMember>> expr)
            => expr.Body switch
            {
                MemberExpression m => m.Member,
                UnaryExpression { Operand: MemberExpression m } => m.Member,
                _ => throw new ArgumentException("Destination must be a member expression, e.g. d => d.UserId")
            };
    }
}
