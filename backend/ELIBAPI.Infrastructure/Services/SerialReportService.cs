using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Xem <see cref="ISerialReportService"/> (tách từ MagazineReportController, port ELIB-LRC 10-04).</summary>
public class SerialReportService(ELIBAPIDbContext db) : ISerialReportService
{
    private const int Received = 1, Claimed = 2, Missing = 3;
    public const int ExportLimit = 100_000;

    /// <summary>Số của đơn đặt còn dùng, lọc theo đơn đặt. Trước đây ô "Mã đơn đặt" chỉ lọc "thuộc một đơn đặt chưa xoá bất kỳ"
    /// — nhập gì cũng ra toàn bộ.</summary>
    private IQueryable<SerialItem> Items(SerialReportFilter f)
    {
        var serials = db.Serials.Where(s => s.IsDelete != 2
            && (f.All || s.TenantId == f.ScopeTenantId || (f.IncludeShared && s.TenantId == null)));
        var key = f.Subscription?.Trim();
        if (!string.IsNullOrEmpty(key))
        {
            if (long.TryParse(key, out var id)) serials = serials.Where(s => s.Id == id);
            else
            {
                var lower = key.ToLower();
                serials = serials.Where(s => db.BibXmls.Any(x => x.BibId == s.BibId && x.Title != null && x.Title.ToLower().Contains(lower)));
            }
        }
        var ids = serials.Select(s => (long?)s.Id);
        return db.SerialItems.AsNoTracking().Where(x => x.IsDelete != 2 && ids.Contains(x.SUBSCRIPTION_ID));
    }

    /// <summary>Ngày "đến" không kèm giờ tính trọn ngày.</summary>
    private static (DateTime? From, DateTime? ToExclusive) Range(SerialReportFilter f) =>
        (f.DateFrom?.Date, f.DateTo is { } to ? (to.TimeOfDay == TimeSpan.Zero ? to.Date.AddDays(1) : to) : null);

    /// <summary>Báo cáo "nhận" lọc theo ngày nhận (PUBLISHED_DATE); trước đây lọc theo ngày dự kiến nên số về trễ sang kỳ sau
    /// không vào báo cáo kỳ nhận.</summary>
    private IQueryable<SerialItem> ReceivedItems(SerialReportFilter f)
    {
        var (from, to) = Range(f);
        var q = Items(f).Where(x => x.STATUS == Received);
        if (from.HasValue) q = q.Where(x => x.PUBLISHED_DATE >= from);
        if (to.HasValue) q = q.Where(x => x.PUBLISHED_DATE < to);
        return q;
    }

    private static (int Page, int Size) Paging(int? pageIndex, int? pageSize) => (Math.Max(pageIndex ?? 1, 1), Math.Clamp(pageSize ?? 20, 1, ExportLimit));

    public async Task<(List<SerialReceivedSummaryRow> Items, int Total)> ReceivedSummaryAsync(SerialReportFilter filter, int? pageIndex, int? pageSize)
    {
        var grouped = await ReceivedItems(filter)
            .GroupBy(x => x.SUBSCRIPTION_ID)
            .Select(g => new { SubscriptionId = g.Key, Quantity = g.Sum(x => x.QUANTITY ?? 1), Issues = g.Count(), Last = g.Max(x => x.PUBLISHED_DATE) })
            .ToListAsync();
        var info = await InfoAsync(grouped.Select(g => g.SubscriptionId));
        var rows = grouped
            .Select(g => { var i = info.GetValueOrDefault(g.SubscriptionId ?? 0); return new SerialReceivedSummaryRow(g.SubscriptionId, i.Title, i.Issn, g.Quantity, g.Issues, g.Last); })
            .OrderBy(r => r.SubscriptionTitle).ThenBy(r => r.SubscriptionId).ToList();
        var (page, size) = Paging(pageIndex, pageSize);
        return (rows.Skip((page - 1) * size).Take(size).ToList(), rows.Count);
    }

    public Task<(List<SerialReportRow> Items, int Total)> ReceivedDetailAsync(SerialReportFilter filter, int? pageIndex, int? pageSize) =>
        PageAsync(ReceivedItems(filter).OrderByDescending(x => x.PUBLISHED_DATE).ThenByDescending(x => x.ID), pageIndex, pageSize);

    /// <summary>Trước đây gồm cả số đã khiếu nại rồi nhận về (STATUS = 1) và bỏ sót số quá hạn chưa khiếu nại, số đánh dấu thiếu.</summary>
    public Task<(List<SerialReportRow> Items, int Total)> MissingClaimAsync(SerialReportFilter filter, int? pageIndex, int? pageSize)
    {
        var today = LibraryClock.Today;
        var (from, to) = Range(filter);
        var q = Items(filter).Where(x => (x.STATUS == null || x.STATUS != Received)
                                         && (x.STATUS == Claimed || x.STATUS == Missing || x.CLAIM_COUNT > 0 || x.PLANNED_DATE < today));
        if (from.HasValue) q = q.Where(x => x.PLANNED_DATE >= from);
        if (to.HasValue) q = q.Where(x => x.PLANNED_DATE < to);
        return PageAsync(q.OrderBy(x => x.PLANNED_DATE).ThenBy(x => x.ID), pageIndex, pageSize);
    }

    private async Task<(List<SerialReportRow> Items, int Total)> PageAsync(IQueryable<SerialItem> query, int? pageIndex, int? pageSize)
    {
        var total = await query.CountAsync();
        var (page, size) = Paging(pageIndex, pageSize);
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync();
        var info = await InfoAsync(items.Select(i => i.SUBSCRIPTION_ID));
        return (items.Select(i =>
        {
            var s = info.GetValueOrDefault(i.SUBSCRIPTION_ID ?? 0);
            return new SerialReportRow(i.ID, i.SUBSCRIPTION_ID, s.Title, s.Issn, i.SERIAL_SEQ, i.PLANNED_DATE, i.PUBLISHED_DATE, i.CLAIM_DATE, i.CLAIM_COUNT, i.STATUS, i.QUANTITY);
        }).ToList(), total);
    }

    /// <summary>Nhan đề (BibXml) và ISSN (MARC 022$a) của đầu báo theo đơn đặt.</summary>
    private async Task<Dictionary<long, (string? Title, string? Issn)>> InfoAsync(IEnumerable<long?> subscriptionIds)
    {
        var ids = subscriptionIds.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var serials = await db.Serials.AsNoTracking().Where(s => ids.Contains(s.Id)).Select(s => new { s.Id, s.BibId }).ToListAsync();
        var bibIds = serials.Where(s => s.BibId.HasValue).Select(s => s.BibId!.Value).Distinct().ToList();
        var titles = await db.BibXmls.AsNoTracking().Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId, x => x.Title);
        var issns = (await db.BibDatas.AsNoTracking()
                .Where(d => d.BibId != null && bibIds.Contains(d.BibId.Value) && d.Field == "022" && d.SubField == "a" && d.IsDelete != 2)
                .OrderBy(d => d.BibDataId).Select(d => new { d.BibId, d.Data }).ToListAsync())
            .GroupBy(d => d.BibId!.Value).ToDictionary(g => g.Key, g => g.First().Data);
        return serials.ToDictionary(s => s.Id,
            s => s.BibId is { } bib ? (titles.GetValueOrDefault(bib), issns.GetValueOrDefault(bib)) : ((string?)null, (string?)null));
    }
}
