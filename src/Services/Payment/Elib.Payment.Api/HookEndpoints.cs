using Elib.Payment.Application;

namespace Elib.Payment.Api;

/// <summary>
/// Webhook công khai của cổng thanh toán (gateway: /hooks/** trên host của từng thư viện → cùng đường dẫn). Không có JWT: đơn vị suy ra từ host
/// qua header X-Tenant-Id có chữ ký gateway; lời gọi được xác thực bằng chữ ký của cổng (VNPAY) hoặc khoá Sepay của chính đơn vị.
/// </summary>
public static class HookEndpoints
{
    public static IEndpointRouteBuilder MapPaymentHooks(this IEndpointRouteBuilder app)
    {
        var hooks = app.MapGroup("/hooks").WithTags("PaymentHooks").AllowAnonymous();

        // VNPAY IPN: GET kèm query string đã ký. Mã trả về là bảng mã của VNPAY (luôn HTTP 200).
        hooks.MapGet("/vnpay/ipn", async (HttpRequest request, HttpContext http, WebhookService s, CancellationToken ct) =>
            Results.Json(await s.VnPayIpnAsync(Query(request), Ip(http), ct), statusCode: StatusCodes.Status200OK));

        // Người dùng được VNPAY chuyển về sau khi trả tiền: kết quả thật do IPN quyết định, trang này chỉ đưa người dùng về OPAC.
        hooks.MapGet("/vnpay/return", () => Results.Redirect("/tai-khoan?payment=return"));

        hooks.MapPost("/sepay", async (SepayWebhook body, HttpContext http, WebhookService s, CancellationToken ct) =>
        {
            var (status, response) = await s.SepayAsync(body, http.Request.Headers.Authorization.ToString(), Ip(http), ct);
            return Results.Json(response, statusCode: status);
        });
        return app;
    }

    private static Dictionary<string, string> Query(HttpRequest request) =>
        request.Query.ToDictionary(kv => kv.Key, kv => kv.Value.ToString(), StringComparer.Ordinal);

    private static string? Ip(HttpContext http) =>
        http.Request.Headers["X-Forwarded-For"].ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
        ?? http.Connection.RemoteIpAddress?.ToString();
}
