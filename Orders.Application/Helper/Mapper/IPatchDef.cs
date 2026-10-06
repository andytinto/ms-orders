namespace Orders.Application.Helpers.Mapper
{
    internal interface IPatchDef
    {
        (Type Src, Type Dest) Key { get; }
        Action<object, object> Compile();
    }
    internal sealed class PatchDef<TSrc, TDest> : IPatchDef
    {
        public (Type Src, Type Dest) Key => (typeof(TSrc), typeof(TDest));

        internal List<Action<TSrc, TDest>> Steps { get; } = new();

        public Action<object, object> Compile()
        {
            // Compile jadi satu delegate untuk performa
            return (srcObj, destObj) =>
            {
                var src = (TSrc)srcObj;
                var dest = (TDest)destObj;

                foreach (var step in Steps)
                    step(src, dest);
            };
        }
    }
}
