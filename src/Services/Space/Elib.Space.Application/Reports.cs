using Elib.BuildingBlocks.Crud;
using Elib.Space.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Space.Application;

public sealed record ReportRequest(DateOnly From, DateOnly To, long? SpaceId = null, int? Category = null);

public sealed record SpaceReportRow(long SpaceId, string SpaceName, int Bookings, int Completed, int NoShow, int Cancelled, double UsedHours);

public sealed record TopReaderRow(string CardNo, string? ReaderName, int Bookings, int NoShow);

public sealed record RoomBookingReport(
    int TotalBookings, int Pending, int Approved, int CheckedIn, int Completed, int Cancelled, int Rejected, int NoShow,
    double NoShowRatePercent, double CancelRatePercent, double TotalUsedHours, int TotalVisitors,
    IReadOnlyList<SpaceReportRow> BySpace, IReadOnlyList<TopReaderRow> TopReaders);

/// <summary>
/// Báo cáo đặt phòng (monolith: RoomBookingReportService, quyền SPACE_REPORT) — tổng hợp theo khoảng ngày đặt (StartAt, giờ VN).
/// Bản gọn hơn bản gốc: bỏ histogram giờ/ngày-trong-tuần, chỉ 1 sheet khi xuất Excel (đủ dùng cho vận hành, có thể bổ sung sau
/// nếu cần).
/// </summary>
public sealed class ReportResource(ICrudDbContext db)
{
    public const int TopReadersLimit = 10;

    public async Task<RoomBookingReport> BuildAsync(ReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var from = ToUtc(request.From);
        var to = ToUtc(request.To.AddDays(1));

        var query = db.Set<RoomBooking>().AsNoTracking().Where(b => b.StartAt >= from && b.StartAt < to);
        if (request.SpaceId is { } spaceId) query = query.Where(b => b.SpaceId == spaceId);
        if (request.Category is { } category)
        {
            var spaceIds = db.Set<BookableSpace>().Where(s => s.Category == category).Select(s => s.Id);
            query = query.Where(b => spaceIds.Contains(b.SpaceId));
        }

        var rows = await query.Select(b => new { b.SpaceId, b.ReaderPublicId, b.CardNo, b.Status, b.PartySize, b.StartAt, b.EndAt, b.CheckedInAt, b.CheckedOutAt }).ToListAsync(ct);
        var total = rows.Count;
        int Count(int status) => rows.Count(r => r.Status == status);
        var completed = Count(RoomBookingStatus.Completed);
        var noShow = Count(RoomBookingStatus.NoShow);
        var cancelled = Count(RoomBookingStatus.Cancelled);
        var usedRows = rows.Where(r => r.Status is RoomBookingStatus.Completed or RoomBookingStatus.CheckedIn).ToList();
        var usedHours = usedRows.Sum(r => ((r.CheckedOutAt ?? r.EndAt) - (r.CheckedInAt ?? r.StartAt)).TotalHours);
        var finishedCount = completed + noShow + Count(RoomBookingStatus.Approved) + Count(RoomBookingStatus.CheckedIn);

        var spaceIds2 = rows.Select(r => r.SpaceId).Distinct().ToList();
        var spaceNames = await db.Set<BookableSpace>().AsNoTracking().Where(s => spaceIds2.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var bySpace = spaceIds2.Select(id =>
        {
            var forSpace = rows.Where(r => r.SpaceId == id).ToList();
            var used = forSpace.Where(r => r.Status is RoomBookingStatus.Completed or RoomBookingStatus.CheckedIn)
                .Sum(r => ((r.CheckedOutAt ?? r.EndAt) - (r.CheckedInAt ?? r.StartAt)).TotalHours);
            return new SpaceReportRow(id, spaceNames.GetValueOrDefault(id, "?"), forSpace.Count, forSpace.Count(r => r.Status == RoomBookingStatus.Completed),
                forSpace.Count(r => r.Status == RoomBookingStatus.NoShow), forSpace.Count(r => r.Status == RoomBookingStatus.Cancelled), Math.Round(used, 1));
        }).OrderByDescending(s => s.Bookings).ToList();

        var readerIds = rows.Select(r => r.ReaderPublicId).Distinct().ToList();
        var readerNames = await db.Set<PatronReplica>().AsNoTracking().Where(r => readerIds.Contains(r.ReaderPublicId)).ToDictionaryAsync(r => r.ReaderPublicId, r => r.FullName, ct);
        var topReaders = rows.GroupBy(r => r.ReaderPublicId)
            .Select(g => new TopReaderRow(g.First().CardNo, readerNames.GetValueOrDefault(g.Key), g.Count(), g.Count(r => r.Status == RoomBookingStatus.NoShow)))
            .OrderByDescending(r => r.Bookings).Take(TopReadersLimit).ToList();

        return new RoomBookingReport(total, Count(RoomBookingStatus.Pending), Count(RoomBookingStatus.Approved), Count(RoomBookingStatus.CheckedIn),
            completed, cancelled, Count(RoomBookingStatus.Rejected), noShow,
            finishedCount == 0 ? 0 : Math.Round(100.0 * noShow / finishedCount, 1),
            total == 0 ? 0 : Math.Round(100.0 * cancelled / total, 1),
            Math.Round(usedHours, 1), rows.Where(r => r.Status is RoomBookingStatus.Completed or RoomBookingStatus.CheckedIn).Sum(r => r.PartySize),
            bySpace, topReaders);
    }

    public async Task<byte[]> ExportAsync(ReportRequest request, CancellationToken ct)
    {
        var report = await BuildAsync(request, ct);
        return CrudExcel.Export<SpaceReportRow>("Báo cáo đặt phòng",
        [
            new("spaceName", "Không gian", r => r.SpaceName),
            new("bookings", "Lượt đặt", r => r.Bookings),
            new("completed", "Hoàn tất", r => r.Completed),
            new("noShow", "Không tới", r => r.NoShow),
            new("cancelled", "Đã huỷ", r => r.Cancelled),
            new("usedHours", "Giờ sử dụng", r => r.UsedHours),
        ], report.BySpace);
    }

    private static DateTimeOffset ToUtc(DateOnly date) => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
}
