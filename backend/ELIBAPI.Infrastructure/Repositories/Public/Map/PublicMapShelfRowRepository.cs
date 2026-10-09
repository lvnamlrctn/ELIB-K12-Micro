using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Map;
using ELIBAPI.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;

namespace ELIBAPI.Infrastructure.Repositories;

public class PublicMapShelfRowSearchRequest : PublicSearchRequest
{
    // Mã phân loại DDC của tài liệu cần tra vị trí kệ (vd "005.133") — so khớp DdcStart <= Ddc <= DdcEnd.
    public string? Ddc { get; set; }
}

public class PublicMapShelfRowRepository : PublicBaseRepository<MapShelfRow, PublicMapShelfRowSearchRequest>
{
    public PublicMapShelfRowRepository(ELIBAPIDbContext db, IMemoryCache cache) : base(db, cache) { }

    // Join tới Object/Floor/Building để gán các field [NotMapped] hiển thị vị trí — không thể lọc
    // sau khi Select() vào 1 type đặt tên (bài học từ lỗi 500 ở StoreBookReportController trước
    // đây), nên toàn bộ join + projection nằm gọn trong 1 IQueryable duy nhất, không compose thêm
    // Where() sau Select() này (PublicBaseRepository chỉ Skip/Take/Count sau BuildQuery — an toàn).
    protected override IQueryable<MapShelfRow> BuildQuery(PublicMapShelfRowSearchRequest r)
    {
        var q =
            from row in _db.MapShelfRows
            join detail in _db.MapShelfDetails on row.ShelfDetailId equals detail.Id
            join obj in _db.MapObjects on detail.ObjectId equals obj.Id
            join floor in _db.MapFloors on obj.FloorId equals floor.Id
            join building in _db.MapBuildings on floor.BuildingId equals building.Id
            where row.IsDelete != 2 && detail.IsDelete != 2 && obj.IsDelete != 2 && obj.Status == 2
                  && floor.IsDelete != 2 && building.IsDelete != 2
            select new { row, detail, obj, floor };

        if (!string.IsNullOrEmpty(r.Ddc))
        {
            var ddc = r.Ddc.Trim();
            q = q.Where(x => string.Compare(x.row.DdcStart, ddc) <= 0 && string.Compare(ddc, x.row.DdcEnd) <= 0);
        }

        return q.Select(x => new MapShelfRow
        {
            Id            = x.row.Id,
            ShelfDetailId = x.row.ShelfDetailId,
            RowIndex      = x.row.RowIndex,
            DdcStart      = x.row.DdcStart,
            DdcEnd        = x.row.DdcEnd,
            Description   = x.row.Description,
            IsDelete      = x.row.IsDelete,
            TenantId      = x.row.TenantId,
            PublicId      = x.row.PublicId,
            ShelfObjectId = x.obj.Id,
            ShelfCode     = x.obj.Code,
            ShelfName     = x.obj.Name,
            FloorNumber   = x.floor.FloorNumber,
            FloorName     = x.floor.Name,
            PositionX     = x.obj.PositionX,
            PositionY     = x.obj.PositionY,
            CategoryRange = x.detail.CategoryRange,
            SubjectName   = x.detail.SubjectName
        });
    }
}
