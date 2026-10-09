using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public interface IPublicLibraryMapRepository
{
    Task<List<PublicMapBuildingResponse>> GetBuildingsAsync();
    Task<List<PublicMapFloorSummaryResponse>> GetFloorsAsync(long buildingId);
    Task<PublicMapFloorResponse?> GetFloorAsync(long floorId);
    Task<PublicBookLocationResponse> LocateBarcodeAsync(long barcodeId);
}

/// <summary>
/// Sơ đồ 2D thư viện cho OPAC công khai — dựng lại từ schema `map` thật đã có sẵn (Building/Floor/Object/
/// ShelfDetail/ShelfRow), cùng công thức join với <see cref="ELIBAPI.API.Controllers.PrintBook.StoreMapShelvingController"/>
/// nhưng mở rộng cho mọi loại MapObject (không chỉ SHELF) vì cần vẽ toàn bộ sơ đồ tầng.
/// </summary>
public class PublicLibraryMapRepository(ELIBAPIDbContext db) : IPublicLibraryMapRepository
{
    public async Task<List<PublicMapBuildingResponse>> GetBuildingsAsync()
    {
        return await db.MapBuildings.AsNoTracking()
            .Where(b => b.IsDelete != 2)
            .Select(b => new PublicMapBuildingResponse { Id = b.Id, Name = b.Name, Code = b.Code })
            .ToListAsync();
    }

    public async Task<List<PublicMapFloorSummaryResponse>> GetFloorsAsync(long buildingId)
    {
        var buildingName = await db.MapBuildings.AsNoTracking()
            .Where(b => b.Id == buildingId).Select(b => b.Name).FirstOrDefaultAsync();

        return await db.MapFloors.AsNoTracking()
            .Where(f => f.BuildingId == buildingId && f.IsDelete != 2)
            .OrderBy(f => f.FloorNumber)
            .Select(f => new PublicMapFloorSummaryResponse
            {
                Id = f.Id, BuildingId = f.BuildingId, BuildingName = buildingName,
                FloorNumber = f.FloorNumber, Name = f.Name,
            })
            .ToListAsync();
    }

    public async Task<PublicMapFloorResponse?> GetFloorAsync(long floorId)
    {
        var floor = await db.MapFloors.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == floorId && f.IsDelete != 2);
        if (floor == null) return null;

        var buildingName = await db.MapBuildings.AsNoTracking()
            .Where(b => b.Id == floor.BuildingId).Select(b => b.Name).FirstOrDefaultAsync();

        var objects = await db.MapObjects.AsNoTracking()
            .Where(o => o.FloorId == floorId && o.IsDelete != 2)
            .ToListAsync();

        var objectIds = objects.Select(o => o.Id).ToList();
        var details = await db.MapShelfDetails.AsNoTracking()
            .Where(d => objectIds.Contains(d.ObjectId) && d.IsDelete != 2)
            .ToListAsync();
        var detailIds = details.Select(d => d.Id).ToList();
        var rows = await db.MapShelfRows.AsNoTracking()
            .Where(r => detailIds.Contains(r.ShelfDetailId) && r.IsDelete != 2)
            .OrderBy(r => r.RowIndex)
            .ToListAsync();

        // Đếm số bản sách thật đang gán vào mỗi giá — không suy diễn sức chứa.
        var occupiedCounts = await db.Barcodes.AsNoTracking()
            .Where(b => b.MapObjectId.HasValue && objectIds.Contains(b.MapObjectId.Value) && b.IsDelete != 2)
            .GroupBy(b => b.MapObjectId!.Value)
            .Select(g => new { ObjectId = g.Key, Count = g.Count() })
            .ToListAsync();

        return new PublicMapFloorResponse
        {
            Id = floor.Id, Name = floor.Name, FloorNumber = floor.FloorNumber,
            Width = floor.Width, Height = floor.Height,
            BuildingId = floor.BuildingId, BuildingName = buildingName,
            Objects = objects.Select(o =>
            {
                var detail = details.FirstOrDefault(d => d.ObjectId == o.Id);
                return new PublicMapObjectResponse
                {
                    Id = o.Id, Name = o.Name, Code = o.Code, ObjectType = o.ObjectType,
                    PositionX = o.PositionX, PositionY = o.PositionY, Width = o.Width, Height = o.Height,
                    ColorHex = o.ColorHex, IconName = o.IconName,
                    ShelfDetail = detail == null ? null : new PublicMapShelfDetailResponse
                    {
                        CategoryRange = detail.CategoryRange,
                        SubjectName   = detail.SubjectName,
                        Capacity      = detail.Capacity,
                        OccupiedCount = occupiedCounts.FirstOrDefault(c => c.ObjectId == o.Id)?.Count ?? 0,
                        Rows = rows.Where(r => r.ShelfDetailId == detail.Id)
                            .Select(r => new PublicMapShelfRowInfo
                            {
                                Id = r.Id, RowIndex = r.RowIndex, DdcStart = r.DdcStart, DdcEnd = r.DdcEnd,
                                Description = r.Description
                            }).ToList(),
                    },
                };
            }).ToList(),
        };
    }

    public async Task<PublicBookLocationResponse> LocateBarcodeAsync(long barcodeId)
    {
        var response = new PublicBookLocationResponse();

        var barcode = await db.Barcodes.AsNoTracking()
            .Where(b => b.Id == barcodeId && b.IsDelete != 2)
            .Select(b => new { b.MapObjectId, b.MapShelfRowId, b.BibId })
            .FirstOrDefaultAsync();
        if (barcode == null) { response.Source = "none"; return response; }

        long? shelfObjectId = null;
        long? shelfRowId    = null;
        var source = "none";

        if (barcode.MapObjectId.HasValue)
        {
            shelfObjectId = barcode.MapObjectId;
            shelfRowId    = barcode.MapShelfRowId;
            source        = "assigned";
        }
        else if (barcode.BibId.HasValue)
        {
            var ddc = await db.BibXmls.AsNoTracking()
                .Where(x => x.BibId == barcode.BibId.Value).Select(x => x.DDC).FirstOrDefaultAsync();
            if (!string.IsNullOrEmpty(ddc))
            {
                var match = await (
                    from row in db.MapShelfRows
                    join detail in db.MapShelfDetails on row.ShelfDetailId equals detail.Id
                    join shelfObj in db.MapObjects on detail.ObjectId equals shelfObj.Id
                    where row.IsDelete != 2 && detail.IsDelete != 2 && shelfObj.IsDelete != 2
                        && !string.IsNullOrEmpty(row.DdcStart) && !string.IsNullOrEmpty(row.DdcEnd)
                        && string.Compare(row.DdcStart, ddc) <= 0 && string.Compare(ddc, row.DdcEnd) <= 0
                    select new { ShelfRowId = row.Id, ShelfObjectId = shelfObj.Id }
                ).FirstOrDefaultAsync();
                if (match != null)
                {
                    shelfObjectId = match.ShelfObjectId;
                    shelfRowId    = match.ShelfRowId;
                    source        = "ddc-range";
                }
            }
        }

        response.Source = source;
        if (shelfObjectId == null) return response;

        var obj = await db.MapObjects.AsNoTracking().FirstOrDefaultAsync(o => o.Id == shelfObjectId.Value);
        if (obj == null) return response;

        var floor = await db.MapFloors.AsNoTracking().FirstOrDefaultAsync(f => f.Id == obj.FloorId);
        var buildingName = floor == null ? null : await db.MapBuildings.AsNoTracking()
            .Where(b => b.Id == floor.BuildingId).Select(b => b.Name).FirstOrDefaultAsync();

        MapShelfRowInfo? rowInfo = null;
        if (shelfRowId.HasValue)
        {
            var r = await db.MapShelfRows.AsNoTracking().FirstOrDefaultAsync(x => x.Id == shelfRowId.Value);
            if (r != null) rowInfo = new MapShelfRowInfo(r.Id, r.RowIndex, r.DdcStart, r.DdcEnd);
        }

        var shelfDetail = await db.MapShelfDetails.AsNoTracking()
            .FirstOrDefaultAsync(d => d.ObjectId == obj.Id && d.IsDelete != 2);

        response.Found         = true;
        response.BuildingId    = floor?.BuildingId;
        response.BuildingName  = buildingName;
        response.FloorId       = floor?.Id;
        response.FloorNumber   = floor?.FloorNumber;
        response.FloorName     = floor?.Name;
        response.FloorWidth    = floor?.Width;
        response.FloorHeight   = floor?.Height;
        response.ShelfObjectId = obj.Id;
        response.ShelfCode     = obj.Code;
        response.ShelfName     = obj.Name;
        response.PositionX     = obj.PositionX;
        response.PositionY     = obj.PositionY;
        response.ShelfRowId    = rowInfo?.Id;
        response.RowIndex      = rowInfo?.RowIndex;
        response.DdcStart      = rowInfo?.DdcStart;
        response.DdcEnd        = rowInfo?.DdcEnd;
        response.CategoryRange = shelfDetail?.CategoryRange;
        response.SubjectName   = shelfDetail?.SubjectName;

        if (floor != null)
        {
            var entrance = await db.MapObjects.AsNoTracking()
                .Where(o => o.FloorId == floor.Id && o.IsDelete != 2 && o.ObjectType == "DOOR")
                .FirstOrDefaultAsync()
                ?? await db.MapObjects.AsNoTracking()
                .Where(o => o.FloorId == floor.Id && o.IsDelete != 2 && o.ObjectType == "ELEVATOR")
                .FirstOrDefaultAsync();
            if (entrance != null)
            {
                response.EntranceObjectId  = entrance.Id;
                response.EntrancePositionX = entrance.PositionX;
                response.EntrancePositionY = entrance.PositionY;
            }
        }

        return response;
    }

    private record MapShelfRowInfo(long Id, int? RowIndex, string? DdcStart, string? DdcEnd);
}
