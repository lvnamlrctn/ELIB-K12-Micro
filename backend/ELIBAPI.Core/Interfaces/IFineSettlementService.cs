namespace ELIBAPI.Core.Interfaces;

/// <summary>Áp dụng effect "đã thu phạt" — dùng chung giữa thao tác thủ công của thủ thư
/// (CirculationFineController.Pay) và cổng thanh toán QR (PaymentService.SettleAsync), tránh trùng logic.</summary>
public interface IFineSettlementService
{
    /// <summary>Đánh dấu 1 khoản phạt (C_Fine) đã thu — idempotent (Returndate đã có thì bỏ qua).</summary>
    Task SettleFineAsync(long fineId, DateTime paidAt, long? updatedBy = null);

    /// <summary>Đánh dấu cả phiếu phạt (C_Fine_Ticket) đã hoàn tất — set Status=2, PaidAmount đủ, và
    /// Returndate cho mọi C_Fine thuộc phiếu còn chưa thu — idempotent.</summary>
    Task SettleTicketAsync(long ticketId, DateTime paidAt, long? updatedBy = null);
}
