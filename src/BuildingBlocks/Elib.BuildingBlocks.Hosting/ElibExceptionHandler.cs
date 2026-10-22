using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Elib.BuildingBlocks.Hosting;

/// <summary>
/// Chuyển exception thành problem+json có <c>code</c> ổn định. Lỗi không lường trước: 500 + INTERNAL_ERROR,
/// không lộ chi tiết ra client (chi tiết nằm trong log theo traceId).
/// </summary>
public sealed partial class ElibExceptionHandler(IProblemDetailsService problemDetails, ILogger<ElibExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, detail) = exception switch
        {
            BusinessRuleException b => (b.StatusCode, b.Code, b.Message),
            TenantRequiredException t => (StatusCodes.Status403Forbidden, "TENANT_REQUIRED", t.Message),
            TenantAccessDeniedException t => (StatusCodes.Status403Forbidden, "TENANT_ACCESS_DENIED", t.Message),
            BadHttpRequestException b => (b.StatusCode, "BAD_REQUEST", b.Message),
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested => (499, "CLIENT_CLOSED", (string?)null),
            _ when IsConcurrencyConflict(exception) => (StatusCodes.Status409Conflict, "CONCURRENCY_CONFLICT",
                "Dữ liệu vừa được người khác thay đổi — hãy tải lại rồi thử lại."),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", (string?)null),
        };

        if (status >= 500) LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path);
        else if (exception is TenantAccessDeniedException) LogTenantDenied(logger, exception.Message, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = status >= 500 ? "Lỗi hệ thống" : null,
                Detail = detail,
                Extensions = { ["code"] = code },
            },
        });
    }

    /// <summary>EF Core DbUpdateConcurrencyException (token đồng thời không khớp) — nhận theo tên để Hosting không phụ thuộc EF.</summary>
    private static bool IsConcurrencyConflict(Exception exception) =>
        exception.GetType().Name == "DbUpdateConcurrencyException" || exception.InnerException is { } inner && IsConcurrencyConflict(inner);

    [LoggerMessage(Level = LogLevel.Error, Message = "Lỗi không xử lý: {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Từ chối truy cập chéo đơn vị: {Reason} ({Path})")]
    private static partial void LogTenantDenied(ILogger logger, string reason, string path);
}
