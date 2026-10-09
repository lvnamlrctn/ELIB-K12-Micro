namespace ELIBAPI.Core.Common;

/// <summary>Loại kết quả nghiệp vụ — controller đổi sang mã HTTP tương ứng (200 / 400 / 404 / 403).</summary>
public enum ServiceStatus { Ok, BadRequest, NotFound, Forbidden }

/// <summary>
/// Kết quả trả về từ tầng service (thay cho việc controller tự truy vấn DbContext rồi tự quyết định mã lỗi).
/// Service chỉ nói "thành công kèm dữ liệu" hoặc "lỗi loại gì + thông báo"; controller lo phần HTTP/ApiResponse.
/// </summary>
public sealed record ServiceResult<T>(ServiceStatus Status, T? Value, string? Error)
{
    public bool IsOk => Status == ServiceStatus.Ok;

    public static ServiceResult<T> Ok(T value)               => new(ServiceStatus.Ok, value, null);
    public static ServiceResult<T> BadRequest(string error)  => new(ServiceStatus.BadRequest, default, error);
    public static ServiceResult<T> NotFound(string error)    => new(ServiceStatus.NotFound, default, error);
    /// <summary>Bị chặn theo nghiệp vụ (vd bản ghi đã khoá) — 403.</summary>
    public static ServiceResult<T> Forbidden(string error)   => new(ServiceStatus.Forbidden, default, error);

    /// <summary>Mã HTTP cụ thể khi nghiệp vụ cần phân biệt kỹ hơn 400/403/404 (vd 401 token hết hạn, 410 hết hạn mượn,
    /// 429 hết lượt mượn đồng thời) — controller trả đúng mã này.</summary>
    public int? HttpStatus { get; init; }

    public static ServiceResult<T> Fail(string error, int httpStatus) => new(ServiceStatus.BadRequest, default, error) { HttpStatus = httpStatus };
}
