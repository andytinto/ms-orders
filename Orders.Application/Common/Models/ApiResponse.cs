namespace Orders.Application.Common.Models
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string ResponseCode { get; set; } = "00";
        public string ResponseMessage { get; set; } = "Success";
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }

        public static ApiResponse<T> Ok(T data, string message = "Success")
        {
            return new ApiResponse<T>
            {
                Success = true,
                ResponseCode = "00",
                ResponseMessage = message,
                Data = data
            };
        }

        public static ApiResponse<T> Fail(string message, string code = "99", List<string>? errors = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                ResponseCode = code,
                ResponseMessage = message,
                Errors = errors
            };
        }
    }

    public class PagedResponse<T>
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
        public List<T> Data { get; set; } = new();
        public bool Success { get; set; } = true;
        public string ResponseCode { get; set; } = "00";
        public string ResponseMessage { get; set; } = "Success";

        public void SetSuccess()
        {
            Success = true;
            ResponseCode = "00";
            ResponseMessage = "Success";
        }
    }

    public class SortingOptions
    {
        public string? Column { get; set; }
        public string? OrderBy { get; set; }
    }

    public interface IStandardListQuery
    {
        int Page { get; set; }
        string? FilterColumn { get; set; }
        string? FilterValue { get; set; }
        SortingOptions? SortBy { get; set; }
    }
}
