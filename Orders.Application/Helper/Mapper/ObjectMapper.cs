namespace Orders.Application.Helpers.Mapper
{
    /// <summary>
    /// Provides high-performance object mapping with compiled mapping expressions.
    /// </summary>
    /// <remarks>
    /// File: ObjectMapper.cs <br/>
    /// Feature: Object Mapping <br/>
    /// Description: Sealed implementation of IObjectMapper that performs object-to-object mapping using pre-compiled LINQ expressions. Supports both regular mapping and patch mapping for partial updates. Validates mappings at construction and provides efficient compiled mapping delegates. <br/>
    /// Usage: Used via IObjectMapper dependency injection for type-safe object transformations. <br/>
    /// Version History: <br/>
    /// - 1.0.0, 2025-05-15, Developer: Initial implementation
    /// </remarks>
    public sealed class ObjectMapper : IObjectMapper
    {
        private readonly Dictionary<(Type Src, Type Dest), Delegate> _compiled;
        private readonly Dictionary<(Type Src, Type Dest), Action<object, object>> _patches;

        internal ObjectMapper(IEnumerable<IMapDef> defs
            , IEnumerable<IPatchDef> patchDefs)
        {
            var duplicates = defs
            .GroupBy(d => d.Key)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.Src.FullName} -> {g.Key.Dest.FullName} (count={g.Count()})")
            .ToList();

            if (duplicates.Count > 0)
                throw new InvalidOperationException("Duplicate mappings found:\n" + string.Join("\n", duplicates));

            _compiled = defs.ToDictionary(d => d.Key, d => d.Compile());

            _patches = patchDefs.ToDictionary(d => d.Key, d => d.Compile());
        }

        public TDest Map<TDest>(object source)
        {
            ArgumentNullException.ThrowIfNull(source);

            var key = (source.GetType(), typeof(TDest));

            if (!_compiled.TryGetValue(key, out var del))
                throw new InvalidOperationException($"No mapping registered: {key.Item1.FullName} -> {key.Item2.FullName}");

            return ((Func<object, TDest>)del)(source);
        }

        public List<TDest> MapList<TSrc, TDest>(IEnumerable<TSrc> source)
        {
            ArgumentNullException.ThrowIfNull(source);

            var key = (typeof(TSrc), typeof(TDest));
            if (!_compiled.TryGetValue(key, out var del))
                throw new InvalidOperationException(
                    $"No mapping registered: {typeof(TSrc).FullName} -> {typeof(TDest).FullName}");

            var map = (Func<object, TDest>)del;

            // pre-allocate kalau source ICollection<T>
            var list = source is ICollection<TSrc> col ? new List<TDest>(col.Count) : new List<TDest>();

            foreach (var item in source)
            {
                if (item is null) continue;
                list.Add(map(item!));
            }

            return list;
        }
        public void Patch<TSrc, TDest>(TSrc source, TDest destination)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(destination);

            var key = (typeof(TSrc), typeof(TDest));

            if (!_patches.TryGetValue(key, out var patch))
                throw new InvalidOperationException(
                    $"No patch mapping registered: {typeof(TSrc).FullName} -> {typeof(TDest).FullName}");

            patch(source!, destination!);
        }
    }
}