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

/// Xếp giá dạng danh sách (không chọn vị trí trên sơ đồ) — dùng chung quyền MAP_SHELVING với
/// StoreMapShelvingController (2 màn hình cùng 1 nghiệp vụ "xếp giá", chỉ khác kiểu giao diện).
[Route("api/PrintBook/Store")]
[Authorize]
public class StoreShelvingController(ELIBAPIDbContext db) : BaseApiController
{
    [HttpPost("SearchUnshelved")]
    [Permission("MAP_SHELVING", "view")]
    public async Task<IActionResult> SearchUnshelved([FromBody] UnshelvedSearchRequest r)
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
        var size  = r.PageSize ?? 10;

        var raw = await query
            .OrderBy(x => x.BarcodeValue)
            .Skip((Math.Max(page, 1) - 1) * size)
            .Take(size)
            .Select(x => new { x.Id, x.BarcodeValue, x.BibId, x.Receipt_Id, x.Store, x.TenantId })
            .ToListAsync();

        // Enrich với bibTitle, receiptCode, storeName, tenantName
        var bibIds      = raw.Where(x => x.BibId.HasValue).Select(x => x.BibId!.Value).Distinct().ToList();
        var receiptIds2 = raw.Where(x => x.Receipt_Id.HasValue).Select(x => x.Receipt_Id!.Value).Distinct().ToList();
        var storeIds    = raw.Where(x => x.Store.HasValue).Select(x => (long)x.Store!.Value).Distinct().ToList();
        var tenantIds   = raw.Where(x => x.TenantId.HasValue).Select(x => x.TenantId!.Value);

        var bibTitles    = await db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId, x => x.Title);
        var receiptNames = await db.AbReceipts.Where(x => receiptIds2.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Receipt_Name);
        var storeNames   = await db.Stores.Where(x => storeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
        var tenantNames  = await TenantScopeHelper.GetTenantNamesAsync(db, tenantIds);

        var items = raw.Select(x => new
        {
            x.Id,
            barcode     = x.BarcodeValue,
            storeId     = x.Store,
            bibTitle    = x.BibId.HasValue      && bibTitles.TryGetValue(x.BibId.Value, out var t)         ? t  : null,
            receiptCode = x.Receipt_Id.HasValue && receiptNames.TryGetValue(x.Receipt_Id.Value, out var rc) ? rc : null,
            storeName   = x.Store.HasValue      && storeNames.TryGetValue((long)x.Store.Value, out var sn)  ? sn : null,
            tenantName  = x.TenantId.HasValue   && tenantNames.TryGetValue(x.TenantId.Value, out var tn)     ? tn : null
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new { items, recordsTotal = total }));
    }

    [HttpPost("Shelve")]
    [Permission("MAP_SHELVING", "edit")]
    public async Task<IActionResult> Shelve([FromBody] ShelveRequest r)
    {
        if (r.BarcodeIds == null || r.BarcodeIds.Count == 0)
            return BadRequest(ApiResponse<string>.Fail("Danh sách barcodeIds không được rỗng"));

        var tenantId = GetTenantId();

        // Tuỳ chọn gán kho cùng lúc xếp giá (port ELIB-LRC 09-23). Kho do client gửi → phải thuộc đơn vị người gọi.
        if (r.StoreId.HasValue && !await db.Stores.AnyAsync(s => s.Id == r.StoreId.Value && s.IsDelete != 2
                && (!tenantId.HasValue || s.TenantId == tenantId)))
            return BadRequest(ApiResponse<string>.Fail("Kho không hợp lệ"));

        var userId   = long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (long?)null;
        var barcodes = await db.Barcodes
            .Where(x => r.BarcodeIds.Contains(x.Id) && x.IsDelete != 2 && (!tenantId.HasValue || x.TenantId == tenantId))
            .ToListAsync();

        var actorName = userId.HasValue
            ? await db.Users.AsNoTracking().Where(u => u.Id == userId.Value).Select(u => u.FullName ?? u.LoginName).FirstOrDefaultAsync()
            : null;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        foreach (var bc in barcodes)
        {
            var oldStatus = bc.Status;
            var oldStore  = bc.Store;
            bc.Status         = "R";
            if (r.StoreId.HasValue) bc.Store = r.StoreId.Value; // không chọn kho → giữ kho cũ như trước
            bc.UpdateRowBy    = userId;
            bc.UpdatedRowDate = DateTime.Now;

            var changes = new List<EntityAuditService.FieldChange>();
            if (oldStatus != bc.Status)
                changes.Add(new EntityAuditService.FieldChange { Field = nameof(Barcode.Status), OldValue = oldStatus, NewValue = bc.Status });
            if (oldStore != bc.Store)
                changes.Add(new EntityAuditService.FieldChange { Field = nameof(Barcode.Store), OldValue = oldStore?.ToString(), NewValue = bc.Store?.ToString() });
            if (changes.Count > 0)
                EntityAuditService.QueueEntityChangeLog(db, "Barcode", bc.PublicId, userId ?? 0, actorName, tenantId, "Shelve",
                    reason: null, changes: changes, ip: ip);
        }
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { success = true, shelved = barcodes.Count }));
    }
}

public class UnshelvedSearchRequest
{
    public string? ReceiptCode { get; set; }
    public string? BarcodeFrom { get; set; }
    public string? BarcodeTo   { get; set; }
    public int?    StoreId     { get; set; }
    public int?    PageIndex   { get; set; }
    public int?    PageSize    { get; set; }
    public Guid?   TenantId    { get; set; }
}

public class ShelveRequest
{
    public List<long> BarcodeIds { get; set; } = [];
    /// <summary>Kho gán cho toàn bộ ĐKCB được xếp giá; null = giữ nguyên kho hiện tại.</summary>
    public int? StoreId { get; set; }
}
