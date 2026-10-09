using System.Security.Claims;
using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/StoreMapShelving")]
[Authorize]
public class StoreMapShelvingController(ELIBAPIDbContext db) : BaseApiController
{
    // ── Danh sách chưa xếp giá (giống StoreShelvingController.SearchUnshelved), có thêm DDC + gợi ý vị trí ──

    [HttpPost("SearchUnshelved")]
    [Permission("MAP_SHELVING", "view")]
    public async Task<IActionResult> SearchUnshelved([FromBody] MapUnshelvedSearchRequest r)
    {
        var jwtTenantId  = GetTenantId();
        var isPrivileged = IsPrivilegedRole();
        var requestTenantId = await TenantScopeHelper.ResolveRequestTenantIdAsync(db, r.TenantId, jwtTenantId, isPrivileged);

        var query = db.Barcodes.Where(x => x.Status == "I" && x.IsDelete != 2);
        query = isPrivileged
            ? (requestTenantId.HasValue ? query.Where(x => x.TenantId == requestTenantId || x.TenantId == null) : query)
            : query.Where(x => x.TenantId == jwtTenantId);

        if (!string.IsNullOrEmpty(r.ReceiptCode))
        {
            var receiptIds = db.AbReceipts
                .Where(x => x.Receipt_Name != null && x.Receipt_Name.Contains(r.ReceiptCode))
                .Select(x => (long?)x.Id);
            query = query.Where(x => receiptIds.Contains(x.Receipt_Id));
        }
        if (!string.IsNullOrEmpty(r.BarcodeFrom))
            query = query.Where(x => string.Compare(x.BarcodeValue, r.BarcodeFrom) >= 0);
        if (!string.IsNullOrEmpty(r.BarcodeTo))
            query = query.Where(x => string.Compare(x.BarcodeValue, r.BarcodeTo) <= 0);
        if (r.StoreId.HasValue)
            query = query.Where(x => x.Store == r.StoreId);

        var total = await query.CountAsync();
        var page  = Math.Max(1, r.PageIndex ?? 1);
        var size  = Math.Clamp(r.PageSize ?? 10, 1, 500);

        var raw = await query
            .OrderBy(x => x.BarcodeValue).ThenBy(x => x.Id)
            .Skip((Math.Max(page, 1) - 1) * size)
            .Take(size)
            .Select(x => new { x.Id, x.BarcodeValue, x.BibId, x.Receipt_Id, x.Store, x.TenantId })
            .ToListAsync();

        var bibIds      = raw.Where(x => x.BibId.HasValue).Select(x => x.BibId!.Value).Distinct().ToList();
        var receiptIds2 = raw.Where(x => x.Receipt_Id.HasValue).Select(x => x.Receipt_Id!.Value).Distinct().ToList();
        var storeIds    = raw.Where(x => x.Store.HasValue).Select(x => (long)x.Store!.Value).Distinct().ToList();
        var tenantIds   = raw.Where(x => x.TenantId.HasValue).Select(x => x.TenantId!.Value);

        var bibInfo      = await db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId, x => new { x.Title, x.DDC });
        var receiptNames = await db.AbReceipts.Where(x => receiptIds2.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Receipt_Name);
        var storeNames   = await db.Stores.Where(x => storeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
        var tenantNames  = await TenantScopeHelper.GetTenantNamesAsync(db, tenantIds);

        // Các ngăn DDC (MapShelfRow) thuộc các giá của kho đang lọc — dùng để gợi ý vị trí theo DDC sách.
        var candidateRows = r.StoreId.HasValue
            ? await (from row in db.MapShelfRows
                     join detail in db.MapShelfDetails on row.ShelfDetailId equals detail.Id
                     join obj in db.MapObjects on detail.ObjectId equals obj.Id
                     where obj.StoreId == r.StoreId && obj.ObjectType == "SHELF" && obj.IsDelete != 2
                           && detail.IsDelete != 2 && row.IsDelete != 2
                     // Nhiều ngăn cùng khớp → luôn chọn cùng 1 ngăn (theo giá rồi thứ tự ngăn).
                     orderby obj.Id, row.RowIndex, row.Id
                     select new
                     {
                         ShelfRowId  = row.Id,
                         MapObjectId = obj.Id,
                         ShelfName   = obj.Name,
                         row.RowIndex,
                         row.DdcStart,
                         row.DdcEnd
                     }).ToListAsync()
            : [];

        var items = raw.Select(x =>
        {
            var ddc = x.BibId.HasValue && bibInfo.TryGetValue(x.BibId.Value, out var bi) ? bi.DDC : null;
            var suggestion = candidateRows.FirstOrDefault(cr => DdcInRange(ddc, cr.DdcStart, cr.DdcEnd));

            return new
            {
                x.Id,
                barcode              = x.BarcodeValue,
                storeId              = x.Store,
                bibTitle             = x.BibId.HasValue && bibInfo.TryGetValue(x.BibId.Value, out var bt) ? bt.Title : null,
                ddc,
                receiptCode          = x.Receipt_Id.HasValue && receiptNames.TryGetValue(x.Receipt_Id.Value, out var rc) ? rc : null,
                storeName            = x.Store.HasValue && storeNames.TryGetValue((long)x.Store.Value, out var sn) ? sn : null,
                tenantName           = x.TenantId.HasValue && tenantNames.TryGetValue(x.TenantId.Value, out var tn) ? tn : null,
                suggestedMapObjectId = suggestion?.MapObjectId,
                suggestedShelfName   = suggestion?.ShelfName,
                suggestedShelfRowId  = suggestion?.ShelfRowId,
                suggestedRowIndex    = suggestion?.RowIndex
            };
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new { items, recordsTotal = total }));
    }

    // ── Cây Tầng → Giá → Ngăn DDC của 1 kho, để vẽ sơ đồ chọn vị trí (chỉ đọc) ──

    [HttpGet("ShelvesByStore/{storeId:long}")]
    [Permission("MAP_SHELVING", "view")]
    public async Task<IActionResult> ShelvesByStore(long storeId)
    {
        var shelves = await db.MapObjects
            .Where(o => o.StoreId == storeId && o.ObjectType == "SHELF" && o.IsDelete != 2)
            .ToListAsync();

        var floorIds = shelves.Select(s => s.FloorId).Distinct().ToList();
        var floors   = await db.MapFloors.Where(f => floorIds.Contains(f.Id) && f.IsDelete != 2).ToListAsync();

        var buildingIds = floors.Select(f => f.BuildingId).Distinct().ToList();
        var buildingNames = await db.MapBuildings
            .Where(b => buildingIds.Contains(b.Id) && b.IsDelete != 2)
            .ToDictionaryAsync(b => b.Id, b => b.Name);

        var shelfIds = shelves.Select(s => s.Id).ToList();
        var details  = await db.MapShelfDetails.Where(d => shelfIds.Contains(d.ObjectId) && d.IsDelete != 2).ToListAsync();
        var detailIds = details.Select(d => d.Id).ToList();
        var rows = await db.MapShelfRows
            .Where(row => detailIds.Contains(row.ShelfDetailId) && row.IsDelete != 2)
            .OrderBy(row => row.RowIndex)
            .ToListAsync();

        var result = floors.Select(f => new
        {
            f.Id,
            f.Name,
            f.FloorNumber,
            f.Width,
            f.Height,
            buildingName = buildingNames.TryGetValue(f.BuildingId, out var bn) ? bn : null,
            shelves = shelves.Where(s => s.FloorId == f.Id).Select(s =>
            {
                var detail = details.FirstOrDefault(d => d.ObjectId == s.Id);
                return new
                {
                    s.Id,
                    s.Name,
                    s.Code,
                    s.PositionX,
                    s.PositionY,
                    s.Width,
                    s.Height,
                    s.ColorHex,
                    s.IconName,
                    shelfDetail = detail == null ? null : new
                    {
                        detail.Id,
                        detail.CategoryRange,
                        detail.SubjectName,
                        detail.Capacity,
                        rows = rows.Where(row => row.ShelfDetailId == detail.Id)
                            .Select(row => new { row.Id, row.RowIndex, row.DdcStart, row.DdcEnd, row.Description })
                            .ToList()
                    }
                };
            }).ToList()
        }).ToList();

        return Ok(ApiResponse<object>.Ok(result));
    }

    // ── Xếp giá: gán vị trí Map + chuyển trạng thái I → R cùng lúc ──

    [HttpPost("Place")]
    [Permission("MAP_SHELVING", "edit")]
    public async Task<IActionResult> Place([FromBody] MapPlaceRequest r)
    {
        if (r.BarcodeIds == null || r.BarcodeIds.Count == 0)
            return BadRequest(ApiResponse<string>.Fail("Danh sách barcodeIds không được rỗng"));

        var tenantId   = GetTenantId();
        var privileged = IsPrivilegedRole();
        // Giá sách phải thuộc đơn vị người gọi (hoặc dùng chung) — trước đây nhận mọi giá / mọi ĐKCB client gửi.
        var mapObject = await db.MapObjects.FirstOrDefaultAsync(o => o.Id == r.MapObjectId && o.IsDelete != 2
            && (privileged || o.TenantId == tenantId || o.TenantId == null));
        if (mapObject == null)
            return BadRequest(ApiResponse<string>.Fail("Không tìm thấy giá sách đã chọn"));

        // Port ELIB-LRC 10-03: ngăn đã chọn phải thuộc đúng giá đã chọn (trước đây ĐKCB có thể gán giá A + ngăn của giá B).
        if (r.MapShelfRowId.HasValue)
        {
            var rowOfShelf = await (from row in db.MapShelfRows
                                    join detail in db.MapShelfDetails on row.ShelfDetailId equals detail.Id
                                    where row.Id == r.MapShelfRowId && row.IsDelete != 2 && detail.ObjectId == mapObject.Id
                                    select row.Id).AnyAsync();
            if (!rowOfShelf) return BadRequest(ApiResponse<string>.Fail("Ngăn đã chọn không thuộc giá sách đã chọn"));
        }

        var userId   = long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (long?)null;
        var barcodes = await db.Barcodes
            .Where(x => r.BarcodeIds.Contains(x.Id) && x.IsDelete != 2 && (privileged || x.TenantId == tenantId))
            .ToListAsync();

        var actorName = userId.HasValue
            ? await db.Users.AsNoTracking().Where(u => u.Id == userId.Value).Select(u => u.FullName ?? u.LoginName).FirstOrDefaultAsync()
            : null;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        foreach (var bc in barcodes)
        {
            var oldMapObjectId = bc.MapObjectId;
            var oldMapShelfRowId = bc.MapShelfRowId;
            var oldStatus = bc.Status;
            bc.MapObjectId    = r.MapObjectId;
            bc.MapShelfRowId  = r.MapShelfRowId;
            // Port ELIB-LRC 10-03: chỉ bản "chưa xếp giá" (I hoặc trống) chuyển sang R; bản đang mượn (B), mất (L), xuất kho
            // (X)… chỉ đổi vị trí, giữ nguyên trạng thái — trước đây mọi bản bị đặt R nên quầy/OPAC coi là còn trên giá.
            if (string.IsNullOrEmpty(bc.Status) || bc.Status == "I") bc.Status = "R";
            bc.UpdateRowBy    = userId;
            bc.UpdatedRowDate = DateTime.Now;

            var changes = new List<EntityAuditService.FieldChange>();
            if (oldMapObjectId != bc.MapObjectId)
                changes.Add(new EntityAuditService.FieldChange { Field = nameof(Barcode.MapObjectId), OldValue = oldMapObjectId?.ToString(), NewValue = bc.MapObjectId?.ToString() });
            if (oldMapShelfRowId != bc.MapShelfRowId)
                changes.Add(new EntityAuditService.FieldChange { Field = nameof(Barcode.MapShelfRowId), OldValue = oldMapShelfRowId?.ToString(), NewValue = bc.MapShelfRowId?.ToString() });
            if (oldStatus != bc.Status)
                changes.Add(new EntityAuditService.FieldChange { Field = nameof(Barcode.Status), OldValue = oldStatus, NewValue = bc.Status });
            if (changes.Count > 0)
                EntityAuditService.QueueEntityChangeLog(db, "Barcode", bc.PublicId, userId ?? 0, actorName, tenantId, "Place", reason: null, changes, ip);
        }
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { success = true, placed = barcodes.Count }));
    }

    /// <summary>DDC của sách có nằm trong ngăn [start, end] không (port ELIB-LRC 10-03). So theo thứ tự ký tự (không phụ
    /// thuộc ngôn ngữ máy chủ; DDC luôn có 3 chữ số đầu nên đúng thứ tự số), và mốc cuối gồm cả phân mục con: ngăn
    /// "330 – 339" nhận cả sách "339.5" — trước đây so sánh thường nên "339.5" > "339" bị loại khỏi gợi ý.</summary>
    public static bool DdcInRange(string? ddc, string? start, string? end)
    {
        if (string.IsNullOrWhiteSpace(ddc) || string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end)) return false;
        var d = ddc.Trim(); var s = start.Trim(); var e = end.Trim();
        return string.CompareOrdinal(d, s) >= 0
            && (string.CompareOrdinal(d, e) <= 0 || d.StartsWith(e, StringComparison.Ordinal));
    }
}

public class MapUnshelvedSearchRequest
{
    public string? ReceiptCode { get; set; }
    public string? BarcodeFrom { get; set; }
    public string? BarcodeTo   { get; set; }
    public int?    StoreId     { get; set; }
    public int?    PageIndex   { get; set; }
    public int?    PageSize    { get; set; }
    public Guid?   TenantId    { get; set; }
}

public class MapPlaceRequest
{
    public List<long> BarcodeIds    { get; set; } = [];
    public long        MapObjectId   { get; set; }
    public long?        MapShelfRowId { get; set; }
}
