using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Payment;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Services.Payment;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Payment;

/// <summary>Tạo/kiểm tra giao dịch thanh toán QR tại quầy (thủ thư chọn phí, sinh QR cho độc giả quét) — port ELIB-LRC 09-25.
/// Xem PaymentReaderController cho luồng tự phục vụ OPAC.
/// K12: quyền theo đúng loại phí (phiếu phạt → FINES, sao chụp → C_PHOTO, cấp lại thẻ → READERS) thay cho mã PAYMENT riêng của LRC —
/// thủ thư thu phạt được thì tạo được QR, không phải cấp thêm quyền; bạn đọc/giao dịch khác đơn vị coi như không tồn tại.</summary>
[Route("api/Payment")]
public class PaymentController(IPaymentService paymentService, IPaymentGatewayResolver gateways, IPermissionService permissions) : BaseApiController
{
    private static string? ModuleOf(string? targetType) => targetType switch
    {
        PaymentService.TargetFineTicket  => "FINES",
        PaymentService.TargetPhotocopy   => "C_PHOTO",
        PaymentService.TargetCardReissue => "READERS",
        _ => null,
    };

    private async Task<bool> CanAsync(string? targetType, string action)
    {
        var module = ModuleOf(targetType);
        return module != null && await permissions.HasPermissionAsync(GetCurrentUserId(), module, action);
    }

    private ObjectResult Forbidden() => StatusCode(403, ApiResponse<object>.Fail("Bạn không có quyền thu loại phí này.", 403));

    /// <summary>Cổng thanh toán đơn vị đã cấu hình — giao diện chỉ hiện lựa chọn dùng được.</summary>
    [HttpGet("Providers")]
    public async Task<IActionResult> Providers() => Ok(ApiResponse<List<string>>.Ok(await gateways.AvailableAsync(GetTenantId())));

    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromBody] CreatePaymentRequest request)
    {
        if (!await CanAsync(request.TargetType, "edit")) return Forbidden();
        try
        {
            var txn = await paymentService.CreateAsync(
                request.ReaderId, request.TargetType, request.TargetId, request.Provider, GetCurrentUserId(), GetTenantId());
            return Ok(ApiResponse<object>.Ok(PaymentResponseMapper.Map(txn)));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("Status/{publicId:guid}")]
    public async Task<IActionResult> Status(Guid publicId)
    {
        var txn = await paymentService.GetByPublicIdAsync(publicId);
        var tenantId = GetTenantId();
        if (txn == null || (tenantId != null && txn.TenantId != tenantId))
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy giao dịch"));
        if (!await CanAsync(txn.TargetType, "view")) return Forbidden();
        return Ok(ApiResponse<object>.Ok(PaymentResponseMapper.Map(txn)));
    }
}

internal static class PaymentResponseMapper
{
    public static object Map(PaymentTransaction t) => new
    {
        t.PublicId,
        t.TransactionCode,
        t.TargetType,
        t.TargetId,
        t.Amount,
        t.Provider,
        t.Status,
        t.QrContent,
        // VNPAY: QrContent là link trang thanh toán — giao diện hiện thêm nút mở link (bạn đọc dùng điện thoại).
        PayUrl    = t.Provider == "VNPAY" ? t.QrContent : null,
        // Lưu theo giờ thư viện; trả UTC ("…Z") để trình duyệt ở múi giờ nào cũng đếm ngược đúng.
        ExpiresAt = DateTime.SpecifyKind(LibraryClock.ToUtc(t.ExpiresAt), DateTimeKind.Utc),
        PaidAt    = t.PaidAt.HasValue ? DateTime.SpecifyKind(LibraryClock.ToUtc(t.PaidAt.Value), DateTimeKind.Utc) : (DateTime?)null,
    };
}
