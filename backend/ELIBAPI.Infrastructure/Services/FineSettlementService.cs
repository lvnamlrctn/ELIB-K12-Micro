using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Effect "đã thu phạt" dùng chung giữa thủ thư (CirculationFineController.Pay) và cổng thanh toán QR (port ELIB-LRC 09-25).
/// Chỉ nhận Id — nơi gọi đã kiểm tra quyền/đơn vị (controller lọc TenantId, PaymentService lấy Id từ giao dịch của chính bạn đọc).</summary>
public class FineSettlementService(ELIBAPIDbContext db) : IFineSettlementService
{
    public async Task SettleFineAsync(long fineId, DateTime paidAt, long? updatedBy = null)
    {
        var fine = await db.CFines.FirstOrDefaultAsync(x => x.Id == fineId && x.IsDelete != 2);
        if (fine == null || fine.Returndate.HasValue) return; // idempotent

        fine.Returndate     = paidAt;
        fine.UpdateRowBy    = updatedBy;
        fine.UpdatedRowDate = paidAt;
        await db.SaveChangesAsync();
    }

    public async Task SettleTicketAsync(long ticketId, DateTime paidAt, long? updatedBy = null)
    {
        var ticket = await db.CFineTickets.FirstOrDefaultAsync(x => x.Id == ticketId && x.IsDelete != 2);
        if (ticket == null || ticket.Status == 2) return; // idempotent

        ticket.Status         = 2;
        ticket.PaidAmount     = (ticket.TotalAmount ?? 0) - (ticket.DiscountAmount ?? 0);
        ticket.UpdateRowBy    = updatedBy;
        ticket.UpdatedRowDate = paidAt;

        var fines = await db.CFines
            .Where(x => x.TicketId == ticketId && x.Returndate == null && x.IsDelete != 2)
            .ToListAsync();
        foreach (var f in fines)
        {
            f.Returndate     = paidAt;
            f.UpdateRowBy    = updatedBy;
            f.UpdatedRowDate = paidAt;
        }

        await db.SaveChangesAsync();
    }
}
