using System.Linq.Expressions;

namespace Orders.Application.Helper
{
    public static class DynamicFilterBuilderHelper
    {
        //public static IQueryable<T> ApplyFilter<T>(this IQueryable<T> query, string? column, string? value)
        //{
        //    if (string.IsNullOrWhiteSpace(column) || string.IsNullOrWhiteSpace(value))
        //        return query;

        //    var param = Expression.Parameter(typeof(T), "x");
        //    var property = Expression.PropertyOrField(param, column);

        //    // pastikan string
        //    if (property.Type != typeof(string))
        //        throw new InvalidOperationException("Filter just for string");

        //    var constant = Expression.Constant(value);
        //    var contains = Expression.Call(
        //        property,
        //        nameof(string.Contains),
        //        Type.EmptyTypes,
        //        constant
        //    );

        //    var lambda = Expression.Lambda<Func<T, bool>>(contains, param);
        //    return query.Where(lambda);
        //}

        public static IQueryable<T> ApplyFilter<T>(
            this IQueryable<T> query,
            string? column,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(column) ||
                string.IsNullOrWhiteSpace(value))
                return query;

            var param = Expression.Parameter(typeof(T), "x");
            var property = Expression.PropertyOrField(param, column);

            Expression body;

            var propertyType =
                Nullable.GetUnderlyingType(property.Type)
                ?? property.Type;

            if (propertyType == typeof(string))
            {
                body = Expression.Call(
                    property,
                    nameof(string.Contains),
                    Type.EmptyTypes,
                    Expression.Constant(value));
            }
            else
            {
                var convertedValue =
                    Convert.ChangeType(value, propertyType);

                body = Expression.Equal(
                    property,
                    Expression.Constant(convertedValue, property.Type));
            }

            var lambda = Expression.Lambda<Func<T, bool>>(body, param);

            return query.Where(lambda);
        }

        public static IQueryable<T> ApplySorting<T>(this IQueryable<T> query, string? column, string? orderBy)
        {
            if (string.IsNullOrWhiteSpace(column)) return query;

            bool isDesc = orderBy?.ToLower() == "desc";
            var param = Expression.Parameter(typeof(T), "x");
            var property = Expression.PropertyOrField(param, column);
            var lambda = Expression.Lambda(property, param);

            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == (isDesc ? "OrderByDescending" : "OrderBy") && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), property.Type);

            return (IQueryable<T>)method.Invoke(null, [query, lambda])!;
        }
    }
}
