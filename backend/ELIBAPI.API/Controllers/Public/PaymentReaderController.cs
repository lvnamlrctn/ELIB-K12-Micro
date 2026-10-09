using System.Security.Claims;
using ELIBAPI.API.Controllers.Payment;
using ELIBAPI.API.Infrastructure;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Public;

/// <summary>Tự phục vụ thanh toán phí/phạt cho độc giả (OPAC) — cùng luồng CreateAsync/SettleAsync với
/// PaymentController phía quầy, chỉ khác cách xác định ReaderId (từ JWT bạn đọc, không nhận từ client). Port ELIB-LRC 09-25.
/// K12: bạn đọc không có claim TenantId — đơn vị lấy từ Reader.TenantId (cấu hình cổng thanh toán của đơn vị đó).</summary>
[ApiController, Authorize, Route("api/public/PaymentReader")]
public class PaymentReaderController(ELIBAPIDbContext db, IPaymentService paymentService, IPaymentGatewayResolver gateways) : ControllerBase
{
    // Cùng cách RoomBookingController/MyLibraryController xác định bạn đọc: chỉ token bạn đọc (Type = "Reader").
    private async Task<(long Id, long? TenantId)?> CurrentReaderAsync()
    {
        if (User.FindFirstValue("Type") != "Reader") return null;
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var publicId)) return null;
        var r = await db.Readers.AsNoTracking().Where(x => x.PublicId == publicId && x.IsDelete != 2)
            .Select(x => new { x.Id, x.TenantId }).FirstOrDefaultAsync();
        return r == null ? null : (r.Id, r.TenantId);
    }

    private async Task<IActionResult> ForReader(Func<(long Id, long? TenantId), Task<IActionResult>> action)
    {
        var reader = await CurrentReaderAsync();
        if (reader == null) return Unauthorized(ApiResponse<object>.Fail("Phiên bạn đọc không hợp lệ.", 401));
        try { return await action(reader.Value); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message)); }
    }

    /// <summary>Khoản phạt/phí chưa thanh toán + cổng đơn vị đã cấu hình — nguồn cho tab "Phí / Nợ của tôi".</summary>
    [HttpGet("MyDebts")]
    public Task<IActionResult> MyDebts() => ForReader(async r => Ok(ApiResponse<object>.Ok(new
    {
        debts = await paymentService.MyDebtsAsync(r.Id),
        providers = await gateways.AvailableAsync(r.TenantId),
    })));

    [HttpPost("Create")]
    [EnableRateLimiting(RateLimitingExtensions.PaymentCreatePolicy)]
    public Task<IActionResult> Create([FromBody] CreateReaderPaymentRequest request) => ForReader(async r =>
    {
        // Bạn đọc chỉ tự trả phạt/sao chụp của chính mình; phí cấp lại thẻ do thủ thư thu tại quầy.
        if (request.TargetType is not (ELIBAPI.Infrastructure.Services.Payment.PaymentService.TargetFineTicket
            or ELIBAPI.Infrastructure.Services.Payment.PaymentService.TargetPhotocopy))
            return BadRequest(ApiResponse<object>.Fail("Loại phí này cần thanh toán tại quầy."));
        var txn = await paymentService.CreateAsync(r.Id, request.TargetType, request.TargetId, request.Provider, null, r.TenantId);
        return Ok(ApiResponse<object>.Ok(PaymentResponseMapper.Map(txn)));
    });

    [HttpGet("Status/{publicId:guid}")]
    public Task<IActionResult> Status(Guid publicId) => ForReader(async r =>
    {
        var txn = await paymentService.GetByPublicIdAsync(publicId);
        if (txn == null || txn.ReaderId != r.Id) return NotFound(ApiResponse<object>.Fail("Không tìm thấy giao dịch"));
        return Ok(ApiResponse<object>.Ok(PaymentResponseMapper.Map(txn)));
    });
}
