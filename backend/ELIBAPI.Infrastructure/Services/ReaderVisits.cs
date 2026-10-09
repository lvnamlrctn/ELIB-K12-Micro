using ELIBAPI.Infrastructure.Data;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Một lượt vào cửa: lượt đã quét ra (<c>CheckOut</c>) hoặc đang ở trong (<c>CheckIn</c> chưa xoá — quét ra sẽ xoá mềm dòng này).</summary>
public sealed class ReaderVisit
{
    public long Id { get; init; }
    public Guid PublicId { get; init; }
    public long? ReaderId { get; init; }
    public DateTime? CheckInTime { get; init; }
    public DateTime? CheckOutTime { get; init; }
    public long? StoreId { get; init; }
    public long? TenantId { get; init; }
}

public static class ReaderVisits
{
    /// <summary>Mọi lượt vào cửa (port ELIB-LRC 10-04) — trước đây lịch sử chỉ đọc lượt đã quét ra nên bỏ sót người đang ở
    /// trong (và người quên quét ra). Có <c>TenantId</c> để bên gọi lọc theo đơn vị.</summary>
    public static IQueryable<ReaderVisit> Query(ELIBAPIDbContext db) =>
        db.CheckOuts.Where(x => x.IsDelete != 2)
            .Select(x => new ReaderVisit { Id = x.Id, PublicId = x.PublicId, ReaderId = x.ReaderId, CheckInTime = x.CheckInTime, CheckOutTime = x.CheckOutTime, StoreId = x.StoreId, TenantId = x.TenantId })
            .Concat(db.CheckIns.Where(x => x.IsDelete != 2)
                .Select(x => new ReaderVisit { Id = x.Id, PublicId = x.PublicId, ReaderId = x.Readerid, CheckInTime = x.CheckInTime, CheckOutTime = null, StoreId = x.StoreId, TenantId = x.TenantId }));
}
