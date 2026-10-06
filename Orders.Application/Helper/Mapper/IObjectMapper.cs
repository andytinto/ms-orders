namespace Orders.Application.Helpers.Mapper
{
    public interface IObjectMapper
    {
        // single object
        TDest Map<TDest>(object source);
        // for list object
        List<TDest> MapList<TSrc, TDest>(IEnumerable<TSrc> source);
        // for pre condition
        void Patch<TSrc, TDest>(TSrc source, TDest destination);
    }
}
