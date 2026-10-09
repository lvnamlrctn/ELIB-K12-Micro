namespace ELIBAPI.Core.Common;

public class ApiResponse<T>
{
    public bool   Success    { get; set; }
    public string Message    { get; set; } = string.Empty;
    public T?     Data       { get; set; }
    public int    StatusCode { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Success.")
        => new() { Success = true, Message = message, Data = data, StatusCode = 200 };

    public static ApiResponse<T> Fail(string message, int statusCode = 400)
        => new() { Success = false, Message = message, StatusCode = statusCode };
}

public class PagedResult<T>
{
    public List<T> Items      { get; set; } = new();
    public int     TotalCount { get; set; }
    public int     PageIndex  { get; set; }
    public int     PageSize   { get; set; }
    public int     TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
