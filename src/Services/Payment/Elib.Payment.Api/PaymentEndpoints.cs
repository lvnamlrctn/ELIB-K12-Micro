using System.Security.Claims;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Payment.Application;

namespace Elib.Payment.Api;

/// <summary>
/// Thanh toán QR của đơn vị. Cán bộ (gateway: /api/admin/payment/** → /api/**): PAYMENT_ACCOUNTS (tài khoản nhận tiền), PAYMENT_TRANSACTIONS
/// (sinh mã, xem giao dịch), PAYMENT_RECONCILIATION (đối soát). Bạn đọc (gateway: /api/opac/payment/** → /api/reader/**): chỉ khoản nợ của chính mình
/// — id bạn đọc lấy từ claim reader_id, không nhận từ client. Cả hai cần license PAYMENT.
/// </summary>
public static class PaymentEndpoints
{
    public const string Module = "PAYMENT";

    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(new RequiresModuleAttribute(Module));

        var account = api.MapGroup("/payment-account").WithTags("PaymentAccount");
        account.MapGet("", [Permission("PAYMENT_ACCOUNTS", "view")] (PaymentAccountService s, CancellationToken ct) => s.GetAsync(ct));
        account.MapPut("", [Permission("PAYMENT_ACCOUNTS", "edit")] (PaymentAccountRequest request, PaymentAccountService s, CancellationToken ct)
            => s.SaveAsync(request, ct));

        api.MapPost("/charges/Resync", [Permission("PAYMENT_RECONCILIATION", "edit")] async (ChargeBackfill b, ITenantContext tenant, CancellationToken ct)
            => new { synced = await b.RunAsync(tenant.RequireTenantId(), force: true, ct) }).WithTags("Charges");

        var payments = api.MapGroup("/payments").WithTags("Payments");
        payments.MapPost("/Create", [Permission("PAYMENT_TRANSACTIONS", "add")] async (CreatePaymentRequest request, HttpContext http, PaymentService s, CancellationToken ct)
            => Results.Json(await s.CreateAsync(request, ContextOf(http), ct), statusCode: StatusCodes.Status201Created));
        payments.MapGet("/Providers", [PermissionAny("PAYMENT_TRANSACTIONS:add", "PAYMENT_TRANSACTIONS:view")] (PaymentService s, CancellationToken ct) => s.EnabledProvidersAsync(ct));
        payments.MapGet("/{publicId:guid}", [PermissionAny("PAYMENT_TRANSACTIONS:add", "PAYMENT_TRANSACTIONS:view")] (Guid publicId, PaymentService s, CancellationToken ct)
            => s.GetAsync(publicId, null, ct));

        payments.MapPost("/Search", [Permission("PAYMENT_TRANSACTIONS", "view")] (PaymentSearch search, PaymentReconciliation r, CancellationToken ct) => r.SearchAsync(search, ct));
        payments.MapPost("/{publicId:guid}/ResolveReview", [Permission("PAYMENT_RECONCILIATION", "edit")] async (Guid publicId, ResolveReviewRequest request, PaymentReconciliation r, CancellationToken ct) =>
        {
            await r.ResolveReviewAsync(publicId, request, ct);
            return Results.NoContent();
        });
        api.MapPost("/webhook-logs/Search", [Permission("PAYMENT_RECONCILIATION", "view")] (WebhookLogSearch search, PaymentReconciliation r, CancellationToken ct) => r.LogsAsync(search, ct))
            .WithTags("WebhookLogs");

        var reader = app.MapGroup("/api/reader").WithTags("ReaderPayments")
            .RequireAuthorization(new RequiresModuleAttribute(Module))
            .RequireAuthorization(new RequireReaderAttribute());
        reader.MapGet("/charges", (ClaimsPrincipal principal, PaymentService s, CancellationToken ct) => s.MyChargesAsync(ReaderId(principal), ct));
        reader.MapPost("/payments/Create", async (ReaderCreatePaymentRequest request, ClaimsPrincipal principal, HttpContext http, PaymentService s, CancellationToken ct)
            => Results.Json(await s.CreateForReaderAsync(request, ReaderId(principal), ContextOf(http), ct), statusCode: StatusCodes.Status201Created));
        reader.MapGet("/payments/{publicId:guid}", (Guid publicId, ClaimsPrincipal principal, PaymentService s, CancellationToken ct)
            => s.GetAsync(publicId, ReaderId(principal), ct));
        return app;
    }

    /// <summary>Địa chỉ IP người thanh toán (VNPAY bắt buộc) và địa chỉ quay lại sau khi trả tiền — cùng host thư viện mà người dùng đang dùng.</summary>
    private static PaymentContext ContextOf(HttpContext http)
    {
        var forwarded = http.Request.Headers["X-Forwarded-For"].ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var ip = forwarded ?? http.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "127.0.0.1";
        var host = http.Request.Headers["X-Forwarded-Host"].ToString() is { Length: > 0 } fh ? fh : http.Request.Host.Value;
        return new PaymentContext(ip, $"https://{host}/hooks/vnpay/return");
    }

    private static Guid ReaderId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ElibClaimTypes.ReaderId), out var id)
            ? id
            : throw new BusinessRuleException(ElibErrorCodes.PermissionDenied, "Token bạn đọc không hợp lệ.", 403);
}
