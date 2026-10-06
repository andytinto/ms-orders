namespace Orders.Application.Helper
{
    public static class GeneralHelper
    {
        public static Guid ToGuidOrEmpty(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? Guid.Empty : Guid.Parse(value);
        }

        public static TimeSpan GetTimezoneOffsetInHours(string? offsetHeader)
        {
            if (string.IsNullOrWhiteSpace(offsetHeader))
                return TimeSpan.Zero;

            var normalized = offsetHeader.StartsWith('+')
                ? offsetHeader[1..] // todo : need test
                : offsetHeader;

            return TimeSpan.TryParse(normalized, out var parsedOffset)
                ? parsedOffset
                : TimeSpan.Zero;
        }

        public static string ExtractCompanyCode(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
                return string.Empty;

            var parts = alias.Split('_', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : alias;
        }

        public static string ToCamelCase(this string input)
        {
            if (string.IsNullOrEmpty(input) || !char.IsUpper(input[0]))
                return input;

            return char.ToLowerInvariant(input[0]) + input[1..];
        }
    }
}
