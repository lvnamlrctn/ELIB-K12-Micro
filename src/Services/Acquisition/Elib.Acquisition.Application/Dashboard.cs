using Elib.Acquisition.Domain;
using Elib.BuildingBlocks.Crud;
using Microsoft.EntityFrameworkCore;

namespace Elib.Acquisition.Application;

public sealed record StatusCount(int Status, string Label, int Count);

public sealed record OrderProgress(int OrderTotal, int ReceiptTotal, IReadOnlyList<StatusCount> OrdersByStatus, IReadOnlyList<StatusCount> ReceiptsByStatus);

public sealed record FundSpend(long FundId, string Name, decimal Budget, decimal Spend);

/// <summary>Ngân sách quỹ so với chi tiêu thực tế. Spend = Σ số lượng × đơn giá × tỷ giá của các dòng phiếu nhận thuộc quỹ (dòng không giá = 0).</summary>
public sealed record BudgetVsSpend(IReadOnlyList<FundSpend> Funds, decimal TotalBudget, decimal TotalSpend);

/// <summary>Bảng điều khiển bổ sung của đơn vị (GET /api/dashboard). Phiếu nhận: 1 = đã hoàn tất, 0 = đang nhập.</summary>
public sealed record AcquisitionDashboard(OrderProgress OrderProgress, BudgetVsSpend BudgetVsSpend);

public sealed class AcquisitionDashboardService(ICrudDbContext db)
{
    private const int RowCap = 100_000;

    public async Task<AcquisitionDashboard> BuildAsync(CancellationToken ct)
    {
        var orderCounts = await db.Set<PurchaseOrder>().AsNoTracking().GroupBy(o => o.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var orders = OrderStatus.Names.Select(s => new StatusCount(s.Key, s.Value, orderCounts.Where(c => c.Status == s.Key).Sum(c => c.Count))).ToList();
        var receiptCounts = await db.Set<Receipt>().AsNoTracking().GroupBy(r => r.CompletedAt != null).Select(g => new { Done = g.Key, Count = g.Count() }).ToListAsync(ct);
        int Done(bool done) => receiptCounts.Where(c => c.Done == done).Sum(c => c.Count);
        StatusCount[] receipts = [new(0, "Đang nhập", Done(false)), new(1, "Đã hoàn tất", Done(true))];

        var funds = await db.Set<Fund>().AsNoTracking().OrderBy(f => f.Name).Select(f => new { f.Id, f.Name, f.Amount }).ToListAsync(ct);
        var lines = await (from l in db.Set<ReceiptLine>().AsNoTracking()
                           join r in db.Set<Receipt>().AsNoTracking() on l.ReceiptId equals r.Id
                           where r.FundId != null && l.Price != null
                           select new { FundId = r.FundId!.Value, l.Quantity, l.Price, l.Rate }).Take(RowCap).ToListAsync(ct);
        // Cộng tiền ở bộ nhớ: SUM decimal không dịch được trên SQLite (test).
        var spend = lines.GroupBy(l => l.FundId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity * (l.Price ?? 0m) * l.Rate));
        var rows = funds.Select(f => new FundSpend(f.Id, f.Name, f.Amount ?? 0m, spend.GetValueOrDefault(f.Id))).ToList();
        return new AcquisitionDashboard(new OrderProgress(orders.Sum(o => o.Count), receipts.Sum(r => r.Count), orders, receipts),
            new BudgetVsSpend(rows, rows.Sum(r => r.Budget), rows.Sum(r => r.Spend)));
    }
}
