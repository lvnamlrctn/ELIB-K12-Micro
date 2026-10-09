using System.Security.Claims;
using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Catalogue/Deliverer")]
public class CatalogueDelivererController : GenericController<AbDeliverer, AbDelivererSearchRequest, AbDelivererRequest>
{
    private readonly ELIBAPIDbContext _db;
    private readonly IElasticsearchService _elastic;
    private readonly IConfiguration _configuration;

    public CatalogueDelivererController(
        IGenericRepository<AbDeliverer, AbDelivererSearchRequest, AbDelivererRequest> repo,
        ELIBAPIDbContext db,
        IElasticsearchService elastic,
        IConfiguration configuration) : base(repo)
    {
        _db = db;
        _elastic = elastic;
        _configuration = configuration;
    }

    private long? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(claim, out var id) ? id : null;
    }

    private long? GetTenantId()
    {
        var claim = User.FindFirstValue("TenantId");
        return long.TryParse(claim, out var id) ? id : null;
    }

    // ── Khoá khi đơn phân bổ đã ký nhận (Sign == 2) ──────────────────────────────
    // Đợt 20: ELIB không có global query filter theo tenant — tự lọc đơn phân bổ theo đơn vị người gọi, nếu không
    // cán bộ đơn vị A ký nhận/thêm/xoá dòng được trên đơn của đơn vị B (tài khoản hệ thống: mọi đơn vị).
    private IQueryable<AbDeliverer> ScopedDeliverers()
    {
        var tenantId = GetTenantId();
        return _db.AbDeliverers.Where(x => x.IsDelete != 2 && (!tenantId.HasValue || x.TenantId == tenantId));
    }

    private async Task<AbDeliverer?> GetDelivererAsync(long? id) =>
        id == null ? null : await ScopedDeliverers().FirstOrDefaultAsync(x => x.Id == id);

    private async Task<AbDeliverer?> GetDelivererByPublicIdAsync(Guid publicId) =>
        await ScopedDeliverers().FirstOrDefaultAsync(x => x.PublicId == publicId);

    /// <summary>Chỉ giữ các ĐKCB thuộc đúng đơn vị của đơn phân bổ — BarcodeId do client gửi, không tin.</summary>
    private async Task<List<long>> OwnBarcodeIdsAsync(IEnumerable<long> ids, long? delivererTenantId)
    {
        var list = ids.Distinct().ToList();
        return await _db.Barcodes
            .Where(x => list.Contains(x.Id) && x.IsDelete != 2 && x.TenantId == delivererTenantId)
            .Select(x => x.Id).ToListAsync();
    }

    private bool IsLocked(AbDeliverer? d) => d != null && d.Sign == 2 && !IsPrivilegedRole();

    private IActionResult LockedResult() =>
        StatusCode(403, ApiResponse<string>.Fail("Đơn phân bổ đã ký nhận, không thể chỉnh sửa."));

    [HttpPost("Add")]
    [Permission("AB_DELIVERERS", "add")]
    public override async Task<IActionResult> Add([FromBody] AbDelivererRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("AB_DELIVERERS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] AbDelivererRequest request)
    {
        if (IsLocked(await GetDelivererByPublicIdAsync(publicId))) return LockedResult();
        return await base.Update(publicId, request);
    }

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("AB_DELIVERERS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId)
    {
        if (IsLocked(await GetDelivererByPublicIdAsync(publicId))) return LockedResult();
        return await base.Delete(publicId);
    }

    [HttpPut("ChangeStatus")]
    [Permission("AB_DELIVERERS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request)
    {
        if (IsLocked(await GetDelivererByPublicIdAsync(request.PublicId))) return LockedResult();
        return await base.ChangeStatus(request);
    }

    [HttpPut("SignConfirm/{publicId:guid}")]
    [Permission("AB_DELIVERERS", "edit")]
    public async Task<IActionResult> SignConfirm(Guid publicId)
    {
        var d = await GetDelivererByPublicIdAsync(publicId);
        if (d == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy đơn phân bổ"));
        if (d.Status != 2) return BadRequest(ApiResponse<string>.Fail("Đơn chưa hoàn thành nhận"));
        if (d.Sign == 2) return BadRequest(ApiResponse<string>.Fail("Đơn đã được ký nhận"));

        d.Sign = 2;
        d.UpdateRowBy = GetUserId();
        d.UpdatedRowDate = DateTime.Now;

        // Port ELIB-LRC 09-23: ký nhận = sách đã về kho nhận → các ĐKCB trong đơn sang "R" (sẵn sàng lưu thông),
        // cùng hành vi với nút "Xếp giá" — trước đây vẫn ở "I" cho tới khi có người xếp giá thủ công. Chỉ đụng
        // ĐKCB cùng đơn vị với ĐƠN PHÂN BỔ, ghi lịch sử theo hồ sơ (action "SignConfirm") cùng lần SaveChanges.
        // Port ELIB-LRC 10-03: chỉ bản "chưa xếp giá" (I hoặc trống) — trước đây mọi ĐKCB khác "R" đều bị đặt "R", kể
        // cả bản đang cho mượn (B), mất (L), thanh lý (S), nên quầy và OPAC coi là còn trên giá.
        var tenantId   = d.TenantId;
        var userId     = GetUserId();
        var barcodeIds = await _db.AbDelivererDetails
            .Where(x => x.Deliverer_Id == d.Id && x.IsDelete != 2 && x.BarcodeId != null)
            .Select(x => x.BarcodeId!.Value).ToListAsync();
        var barcodes = await _db.Barcodes
            .Where(x => barcodeIds.Contains(x.Id) && x.IsDelete != 2 && x.TenantId == tenantId
                     && (x.Status == null || x.Status == "" || x.Status == "I"))
            .ToListAsync();
        if (barcodes.Count > 0)
        {
            var actorName = userId.HasValue
                ? await _db.Users.AsNoTracking().Where(u => u.Id == userId.Value).Select(u => u.FullName ?? u.LoginName).FirstOrDefaultAsync()
                : null;
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            foreach (var bc in barcodes)
            {
                var oldStatus = bc.Status;
                bc.Status = "R";
                bc.UpdateRowBy = userId;
                bc.UpdatedRowDate = DateTime.Now;
                EntityAuditService.QueueEntityChangeLog(_db, "Barcode", bc.PublicId, userId ?? 0, actorName, tenantId, "SignConfirm",
                    reason: null,
                    changes: [new EntityAuditService.FieldChange { Field = nameof(Barcode.Status), OldValue = oldStatus, NewValue = bc.Status }],
                    ip: ip);
            }
        }
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Đã ký nhận đơn phân bổ"));
    }

    [HttpGet("CanEdit/{publicId:guid}")]
    [Permission("AB_DELIVERERS", "view")]
    public async Task<IActionResult> CanEdit(Guid publicId)
    {
        var d = await GetDelivererByPublicIdAsync(publicId);
        var locked = d != null && d.Sign == 2;
        return Ok(ApiResponse<object>.Ok(new { locked, canEdit = !locked || IsPrivilegedRole() }));
    }

    [HttpGet("{id:long}")]
    [Permission("AB_DELIVERERS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("AB_DELIVERERS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("AB_DELIVERERS", "view")]
    public override async Task<IActionResult> Search([FromBody] AbDelivererSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("AB_DELIVERERS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] AbDelivererSearchRequest request) => await base.SearchAll(request);

    [HttpGet("Lines/{delivererId:long}")]
    [Permission("AB_DELIVERERS", "view")]
    public async Task<IActionResult> Lines(long delivererId)
    {
        var tenantId = GetTenantId();
        var delivererExists = await _db.AbDeliverers.AnyAsync(x => x.Id == delivererId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (!delivererExists) return NotFound(ApiResponse<string>.Fail("Không tìm thấy đơn phân bổ"));

        var lines = await _db.AbDelivererDetails
            .Where(x => x.Deliverer_Id == delivererId && x.IsDelete != 2)
            .ToListAsync();

        var barcodeIds = lines.Where(x => x.BarcodeId.HasValue).Select(x => x.BarcodeId!.Value).Distinct().ToList();
        var barcodeMap = await _db.Barcodes
            .Where(x => barcodeIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        var bibIds = barcodeMap.Values.Where(b => b.BibId.HasValue).Select(b => b.BibId!.Value).Distinct().ToList();
        var mfnMap = await _db.Bibs
            .Where(x => bibIds.Contains(x.Bibid))
            .ToDictionaryAsync(x => x.Bibid, x => x.Mfn);
        var xmlMap = await _db.BibXmls
            .Where(x => bibIds.Contains(x.BibId))
            .ToDictionaryAsync(x => x.BibId);

        var result = lines.Select(l =>
        {
            Barcode? bc = null;
            if (l.BarcodeId.HasValue) barcodeMap.TryGetValue(l.BarcodeId.Value, out bc);
            long? mfn = null; BibXml? xml = null;
            if (bc?.BibId != null)
            {
                mfnMap.TryGetValue(bc.BibId.Value, out mfn);
                xmlMap.TryGetValue(bc.BibId.Value, out xml);
            }
            return new
            {
                l.Id, l.Deliverer_Id, l.BarcodeId, l.Store_Id, l.PublicId,
                BarcodeValue = bc?.BarcodeValue,
                Mfn          = mfn,
                Title        = xml?.Title,
                Author       = xml?.Author,
                Publisher    = xml?.Publisher,
                PublishDate  = xml?.PublishDate
            };
        }).ToList();

        return Ok(ApiResponse<object>.Ok(result));
    }

    // Liệt kê sách thuộc đúng đơn nhận (Receipt_Id), loại trừ sách đã được phân bổ cho bất kỳ đơn
    // phân bổ nào khác (tham khảo nghiệp vụ ở stored procedure cũ).
    [HttpPost("SearchBarcode")]
    [Permission("AB_DELIVERERS", "view")]
    public async Task<IActionResult> SearchBarcode([FromBody] DelivererSearchBarcodeRequest r)
    {
        if (r.ReceiptId == null)
            return Ok(ApiResponse<object>.Ok(new { items = Array.Empty<object>(), totalCount = 0 }));

        var tenantId = GetTenantId();
        var receipt = await _db.AbReceipts.Where(x => x.Id == r.ReceiptId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId)).Select(x => new { x.TenantId }).FirstOrDefaultAsync();
        if (receipt == null)
            return Ok(ApiResponse<object>.Ok(new { items = Array.Empty<object>(), totalCount = 0 }));

        // Sách thuộc đơn nhận có thể đăng ký ĐKCB qua 2 luồng: (1) qua dòng phiếu nhập — Barcode được
        // gán sẵn Receipt_Id, hoặc (2) trực tiếp từ trang biên mục (catalog-bibs), không qua phiếu
        // nhập — Barcode không có Receipt_Id. Phải hợp cả 2 nguồn để không bỏ sót sách đăng ký qua (2).
        var bibIdsInReceipt = await _db.AbReceiptDetails
            .Where(x => x.Receipt_Id == r.ReceiptId && x.IsDelete != 2 && x.Bibid.HasValue)
            .Select(x => x.Bibid!.Value)
            .ToListAsync();

        // Tenant: chỉ ĐKCB cùng đơn vị với đơn nhận (biểu ghi dùng chung có ĐKCB ở nhiều đơn vị).
        var q = _db.Barcodes.Where(x => x.IsDelete != 2 && x.TenantId == receipt.TenantId
            && (x.Receipt_Id == r.ReceiptId || (x.BibId.HasValue && bibIdsInReceipt.Contains(x.BibId.Value)))
            && !_db.AbDelivererDetails.Where(d => d.IsDelete != 2).Select(d => d.BarcodeId).Contains(x.Id));

        if (string.Equals(r.SearchBy, "mfn", StringComparison.OrdinalIgnoreCase) && long.TryParse(r.Keyword, out var mfnVal))
        {
            var bibIdsForMfn = await _db.Bibs.Where(b => b.Mfn == mfnVal && b.IsDelete != 2).Select(b => b.Bibid).ToListAsync();
            q = q.Where(x => x.BibId.HasValue && bibIdsForMfn.Contains(x.BibId.Value));
        }
        else if (!string.IsNullOrWhiteSpace(r.Keyword))
        {
            q = q.Where(x => x.BarcodeValue!.Contains(r.Keyword));
        }

        if (!string.IsNullOrWhiteSpace(r.Barcode)) q = q.Where(x => x.BarcodeValue!.Contains(r.Barcode));

        var matchedBibIds = await BibFilterHelper.GetMatchingBibIdsAsync(_db, r.MfnFrom, r.MfnTo, r.Title, r.Author, r.Publisher, r.PublishDate,
            _elastic, _configuration);
        if (matchedBibIds != null) q = q.Where(x => x.BibId.HasValue && matchedBibIds.Contains(x.BibId.Value));

        var totalCount = await q.CountAsync();
        var page = Math.Max(1, r.PageIndex);
        var size = r.PageSize <= 0 ? 20 : Math.Min(r.PageSize, 500);
        var barcodes = await q.OrderByDescending(x => x.Id).Skip((Math.Max(page, 1) - 1) * size).Take(size).ToListAsync();

        var bibIds = barcodes.Where(x => x.BibId.HasValue).Select(x => x.BibId!.Value).Distinct().ToList();
        var mfnMap = await _db.Bibs.Where(x => bibIds.Contains(x.Bibid)).ToDictionaryAsync(x => x.Bibid, x => x.Mfn);
        var xmlMap = await _db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId);

        var items = barcodes.Select(bc =>
        {
            long? mfn = null; BibXml? xml = null;
            if (bc.BibId.HasValue)
            {
                mfnMap.TryGetValue(bc.BibId.Value, out mfn);
                xmlMap.TryGetValue(bc.BibId.Value, out xml);
            }
            return new
            {
                BarcodeId    = bc.Id,
                BarcodeValue = bc.BarcodeValue,
                Mfn          = mfn,
                Title        = xml?.Title,
                Author       = xml?.Author,
                Publisher    = xml?.Publisher,
                PublishDate  = xml?.PublishDate,
                Store        = bc.Store
            };
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new { items, totalCount }));
    }

    [HttpPost("AddLine")]
    [Permission("AB_DELIVERERS", "edit")]
    public async Task<IActionResult> AddLine([FromBody] AbDelivererDetailRequest r)
    {
        if (r.Deliverer_Id == null || r.BarcodeId == null)
            return BadRequest(ApiResponse<string>.Fail("Deliverer_Id/BarcodeId là bắt buộc"));
        var deliverer = await GetDelivererAsync(r.Deliverer_Id);
        if (deliverer == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy đơn phân bổ"));
        if (IsLocked(deliverer)) return LockedResult();

        var exists = await _db.AbDelivererDetails.AnyAsync(x =>
            x.Deliverer_Id == r.Deliverer_Id && x.BarcodeId == r.BarcodeId && x.IsDelete != 2);
        if (exists) return BadRequest(ApiResponse<string>.Fail("Sách này đã có trong đơn"));

        var error = await AddBarcodesAsync(deliverer, [r.BarcodeId.Value], r.Store_Id);
        if (error != null) return BadRequest(ApiResponse<string>.Fail(error));
        return Ok(ApiResponse<string>.Ok("Đã thêm sách vào đơn"));
    }

    /// <summary>Thêm nhiều sách vào đơn phân bổ 1 lần (port ELIB-LRC 09-16, đa chọn ở màn chọn sách). Bỏ qua ĐKCB
    /// đã có trong đơn; trả số dòng thêm thật.</summary>
    [HttpPost("AddLines")]
    [Permission("AB_DELIVERERS", "edit")]
    public async Task<IActionResult> AddLines([FromBody] DelivererAddLinesRequest r)
    {
        if (r.BarcodeIds == null || r.BarcodeIds.Count == 0)
            return BadRequest(ApiResponse<string>.Fail("Danh sách sách không được rỗng"));
        var deliverer = await GetDelivererAsync(r.DelivererId);
        if (deliverer == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy đơn phân bổ"));
        if (IsLocked(deliverer)) return LockedResult();

        var requested = r.BarcodeIds.Distinct().ToList();
        var existingIds = await _db.AbDelivererDetails
            .Where(x => x.Deliverer_Id == deliverer.Id && x.IsDelete != 2 && x.BarcodeId.HasValue)
            .Select(x => x.BarcodeId!.Value).ToListAsync();
        var toAdd = requested.Except(existingIds).ToList();

        var error = await AddBarcodesAsync(deliverer, toAdd, r.Store_Id);
        if (error != null) return BadRequest(ApiResponse<string>.Fail(error));
        return Ok(ApiResponse<object>.Ok(new { success = true, added = toAdd.Count, skipped = requested.Count - toAdd.Count }));
    }

    /// <summary>Thêm các ĐKCB vào đơn, lưu 1 lần cho cả lô (port ELIB-LRC 10-03). Từ chối CẢ LÔ khi có ĐKCB:
    /// <list type="bullet">
    /// <item>không tồn tại, đã xoá, hoặc khác đơn vị của đơn phân bổ (trước đây bỏ qua im lặng);</item>
    /// <item>đã nằm ở đơn phân bổ khác chưa xoá — trước đây 2 người cùng chọn 1 cuốn thì 1 ĐKCB nằm ở 2 đơn.</item>
    /// </list>
    /// Trả null khi thành công, ngược lại là thông báo lỗi.</summary>
    private async Task<string?> AddBarcodesAsync(AbDeliverer deliverer, List<long> barcodeIds, int? storeId)
    {
        if (barcodeIds.Count == 0) return null;

        var owned = await OwnBarcodeIdsAsync(barcodeIds, deliverer.TenantId);
        var invalid = barcodeIds.Except(owned).ToList();
        if (invalid.Count > 0)
            return $"{invalid.Count} ĐKCB không tồn tại, đã xoá hoặc thuộc đơn vị khác";

        var liveDeliverers = _db.AbDeliverers.Where(x => x.IsDelete != 2 && x.Id != deliverer.Id).Select(x => x.Id);
        var taken = await _db.AbDelivererDetails
            .Where(x => x.IsDelete != 2 && x.BarcodeId != null && barcodeIds.Contains(x.BarcodeId.Value)
                     && x.Deliverer_Id != null && liveDeliverers.Contains(x.Deliverer_Id.Value))
            .Join(_db.Barcodes, x => x.BarcodeId, b => b.Id, (x, b) => b.BarcodeValue)
            .Distinct().Take(10).ToListAsync();
        if (taken.Count > 0)
            return $"ĐKCB đã nằm ở đơn phân bổ khác: {string.Join(", ", taken)}";

        var userId = GetUserId();
        foreach (var barcodeId in barcodeIds)
            _db.AbDelivererDetails.Add(new AbDelivererDetail
            {
                Deliverer_Id   = deliverer.Id,
                BarcodeId      = barcodeId,
                Store_Id       = storeId,
                PublicId       = Guid.NewGuid(),
                TenantId       = deliverer.TenantId,
                CreatedRowBy   = userId,
                CreatedRowDate = DateTime.Now
            });
        await _db.SaveChangesAsync();
        return null;
    }

    [HttpDelete("DeleteLine/{lineId:long}")]
    [Permission("AB_DELIVERERS", "edit")]
    public async Task<IActionResult> DeleteLine(long lineId)
    {
        var tenantId = GetTenantId();
        var line = await _db.AbDelivererDetails.FirstOrDefaultAsync(x => x.Id == lineId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (line == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy dòng"));
        if (IsLocked(await GetDelivererAsync(line.Deliverer_Id))) return LockedResult();

        line.IsDelete       = 2;
        line.UpdateRowBy    = GetUserId();
        line.UpdatedRowDate = DateTime.Now;
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Đã xóa sách khỏi đơn"));
    }
}

public class DelivererAddLinesRequest
{
    public long       DelivererId { get; set; }
    public List<long> BarcodeIds  { get; set; } = [];
    public int?       Store_Id    { get; set; }
}

public class DelivererSearchBarcodeRequest
{
    public long?   ReceiptId   { get; set; } // id đơn nhận cần liệt kê sách chưa phân bổ
    public string  SearchBy    { get; set; } = "mfn"; // "mfn" | "barcode"
    public string? Keyword     { get; set; }
    public string? Barcode     { get; set; }
    public string? Title       { get; set; }
    public string? Author      { get; set; }
    public string? Publisher   { get; set; }
    public string? PublishDate { get; set; }
    public long?   MfnFrom     { get; set; }
    public long?   MfnTo       { get; set; }
    public int     PageIndex   { get; set; } = 1;
    public int     PageSize    { get; set; } = 20;
}
