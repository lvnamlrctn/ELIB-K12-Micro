using ELIBAPI.Core.Common;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Infrastructure;

/// <summary>Đổi <see cref="ServiceResult{T}"/> từ tầng service sang phản hồi HTTP theo đúng khuôn ApiResponse cũ:
/// 200 + <c>ApiResponse.Ok(data)</c>, 400/404/403 + <c>ApiResponse.Fail(thông báo)</c>.</summary>
public static class ServiceResultExtensions
{
    public static IActionResult FromServiceResult<T, TOut>(this ControllerBase controller, ServiceResult<T> result, Func<T, TOut> toResponse) =>
        result.HttpStatus is int code && !result.IsOk
            ? controller.StatusCode(code, ApiResponse<object>.Fail(result.Error ?? "Yêu cầu không hợp lệ", code))
            : result.Status switch
        {
            ServiceStatus.Ok       => controller.Ok(ApiResponse<TOut>.Ok(toResponse(result.Value!))),
            ServiceStatus.NotFound => controller.NotFound(ApiResponse<string>.Fail(result.Error ?? "Không tìm thấy dữ liệu", 404)),
            ServiceStatus.Forbidden => controller.StatusCode(403, ApiResponse<string>.Fail(result.Error ?? "Không được phép thao tác", 403)),
            _                      => controller.BadRequest(ApiResponse<string>.Fail(result.Error ?? "Yêu cầu không hợp lệ")),
        };
}
