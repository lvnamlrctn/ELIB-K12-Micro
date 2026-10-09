using System.Security.Claims;
using ClosedXML.Excel;
using ELIBAPI.API.Filters;
using ELIBAPI.API.Helpers;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Catalogue/Receipt")]
public class CatalogueReceiptController : GenericController<AbReceipt, AbReceiptSearchRequest, AbReceiptRequest>
{
    private readonly ELIBAPIDbContext _db;

    public CatalogueReceiptController(
        IGenericRepository<AbReceipt, AbReceiptSearchRequest, AbReceiptRequest> repo,
        ELIBAPIDbContext db) : base(repo) => _db = db;

    private long? GetUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(claim, out var id) ? id : null;
    }

    private long? GetTenantId()
    {
        var claim = User.FindFirstValue("TenantId");
        return long.TryParse(claim, out var id) ? id : null;
    }

    // ── Khoá khi đơn nhận đã "Hoàn tất" (Status == 2) ────────────────────────────
    private async Task<AbReceipt?> GetReceiptAsync(long? id) =>
        id == null ? null : await _db.AbReceipts.FirstOrDefaultAsync(x => x.Id == id && x.IsDelete != 2);

    private async Task<AbReceipt?> GetReceiptByPublicIdAsync(Guid publicId) =>
        await _db.AbReceipts.FirstOrDefaultAsync(x => x.PublicId == publicId && x.IsDelete != 2);

    // Đợt 20: ELIB không có global query filter theo tenant — dòng đơn nhận phải tự lọc theo đơn vị người gọi,
    // nếu không cán bộ đơn vị A thao tác được dòng của đơn vị B qua Id (tài khoản hệ thống: mọi đơn vị).
    private IQueryable<AbReceiptDetail> ScopedLines()
    {
        var tenantId = GetTenantId();
        return _db.AbReceiptDetails.Where(x => x.IsDelete != 2 && (!tenantId.HasValue || x.TenantId == tenantId));
    }

    // Số ĐKCB đã đăng ký cho 1 dòng đơn nhận (cùng đơn nhận + biểu ghi + đơn vị).
    private Task<int> CountLineBarcodesAsync(AbReceiptDetail line) =>
        _db.Barcodes.CountAsync(x => x.Receipt_Id == line.Receipt_Id && x.BibId == line.Bibid
                                  && x.TenantId == line.TenantId && x.IsDelete != 2);

    private bool IsLocked(AbReceipt? r) => r != null && r.Status == 2 && !IsPrivilegedRole();

    private IActionResult LockedResult() =>
        StatusCode(403, ApiResponse<string>.Fail("Đơn nhận đã hoàn tất, không thể chỉnh sửa."));

    [HttpPost("Add")]
    [Permission("AB_RECEIPTS", "add")]
    public override async Task<IActionResult> Add([FromBody] AbReceiptRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("AB_RECEIPTS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] AbReceiptRequest request)
    {
        if (IsLocked(await GetReceiptByPublicIdAsync(publicId))) return LockedResult();
        return await base.Update(publicId, request);
    }

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("AB_RECEIPTS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId)
    {
        if (IsLocked(await GetReceiptByPublicIdAsync(publicId))) return LockedResult();
        return await base.Delete(publicId);
    }

    [HttpPut("ChangeStatus")]
    [Permission("AB_RECEIPTS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request)
    {
        if (IsLocked(await GetReceiptByPublicIdAsync(request.PublicId))) return LockedResult();
        return await base.ChangeStatus(request);
    }

    [HttpGet("CanEdit/{publicId:guid}")]
    [Permission("AB_RECEIPTS", "view")]
    public async Task<IActionResult> CanEdit(Guid publicId)
    {
        var r = await GetReceiptByPublicIdAsync(publicId);
        var locked = r != null && r.Status == 2;
        return Ok(ApiResponse<object>.Ok(new { locked, canEdit = !locked || IsPrivilegedRole() }));
    }

    [HttpGet("{id:long}")]
    [Permission("AB_RECEIPTS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("AB_RECEIPTS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("AB_RECEIPTS", "view")]
    public override async Task<IActionResult> Search([FromBody] AbReceiptSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("AB_RECEIPTS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] AbReceiptSearchRequest request) => await base.SearchAll(request);

    // Đơn nhận đã "Hoàn tất" (Status=2) và còn ít nhất 1 sách chưa phân bổ cho đơn phân bổ nào — dùng
    // cho dropdown "Đơn nhận" ở trang chi tiết Đơn phân bổ (tham khảo nghiệp vụ ở stored procedure cũ).
    // Bắt cả 2 nguồn ĐKCB (Barcode.Receipt_Id lẫn Barcode chỉ có BibId qua ab_receipt_detail) — cùng
    // logic đã dùng ở CatalogueDelivererController.SearchBarcode để nhất quán.
    [HttpGet("SearchAvailableForDeliverer")]
    [Permission("AB_RECEIPTS", "view")]
    public async Task<IActionResult> SearchAvailableForDeliverer()
    {
        var allocatedBarcodeIds = _db.AbDelivererDetails.Where(d => d.IsDelete != 2).Select(d => d.BarcodeId);

        var receiptIdsFromBarcode = await _db.Barcodes
            .Where(bc => bc.IsDelete != 2 && bc.Receipt_Id != null && !allocatedBarcodeIds.Contains(bc.Id))
            .Select(bc => bc.Receipt_Id!.Value)
            .Distinct()
            .ToListAsync();

        var bibIdsWithStock = await _db.Barcodes
            .Where(bc => bc.IsDelete != 2 && bc.BibId != null && !allocatedBarcodeIds.Contains(bc.Id))
            .Select(bc => bc.BibId!.Value)
            .Distinct()
            .ToListAsync();

        var receiptIdsFromDetail = await _db.AbReceiptDetails
            .Where(d => d.IsDelete != 2 && d.Receipt_Id != null && d.Bibid.HasValue && bibIdsWithStock.Contains(d.Bibid.Value))
            .Select(d => d.Receipt_Id!.Value)
            .Distinct()
            .ToListAsync();

        var eligibleReceiptIds = receiptIdsFromBarcode.Union(receiptIdsFromDetail).ToList();
        var tenantId = GetTenantId();

        var receipts = await _db.AbReceipts
            .Where(x => x.IsDelete != 2 && x.Status == 2 && eligibleReceiptIds.Contains(x.Id)
                && (!tenantId.HasValue || x.TenantId == tenantId))
            .OrderBy(x => x.Id)
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { items = receipts, totalCount = receipts.Count }));
    }

    [HttpGet("Lines/{receiptId:long}")]
    [Permission("AB_RECEIPTS", "view")]
    public async Task<IActionResult> Lines(long receiptId)
    {
        var tenantId = GetTenantId();
        var receiptExists = await _db.AbReceipts.AnyAsync(x => x.Id == receiptId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (!receiptExists) return NotFound(ApiResponse<string>.Fail("Không tìm thấy đơn nhận"));

        var lines = await _db.AbReceiptDetails
            .Where(x => x.Receipt_Id == receiptId && x.IsDelete != 2)
            .ToListAsync();

        var bibIds = lines.Where(x => x.Bibid.HasValue).Select(x => x.Bibid!.Value).Distinct().ToList();
        var xmlMap = await _db.BibXmls
            .Where(x => bibIds.Contains(x.BibId))
            .ToDictionaryAsync(x => x.BibId);
        var bibMap = await _db.Bibs
            .Where(x => bibIds.Contains(x.Bibid))
            .Select(x => new { x.Bibid, x.Mfn, x.PublicId })
            .ToDictionaryAsync(x => x.Bibid);

        // Tính registerCount (số KCB đã đăng ký) theo bibId trong phiếu
        var barcodeCountMap = await _db.Barcodes
            .Where(x => x.Receipt_Id == receiptId && x.IsDelete != 2 && x.BibId != null)
            .GroupBy(x => x.BibId!.Value)
            .Select(g => new { BibId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BibId, x => x.Count);

        var result = lines.Select(l =>
        {
            xmlMap.TryGetValue(l.Bibid ?? 0, out var xml);
            bibMap.TryGetValue(l.Bibid ?? 0, out var bib);
            return new
            {
                l.Id, l.Receipt_Id, l.Bibid, l.Amount, l.Price, l.CURRENCY, l.Rate,
                l.Order_Id, l.PublicId,
                Mfn = bib?.Mfn,
                // Bib.PublicId — để in nhãn môn loại theo API ClassLabel/SearchByBib (port ELIB-LRC 09-23).
                BibPublicId = bib?.PublicId,
                Title = xml?.Title, Author = xml?.Author, Publisher = xml?.Publisher, PublishDate = xml?.PublishDate,
                RegisterCount = l.Bibid.HasValue && barcodeCountMap.TryGetValue(l.Bibid.Value, out var c) ? c : 0
            };
        }).ToList();

        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost("SaveDetail")]
    [Permission("AB_RECEIPTS", "edit")]
    public async Task<IActionResult> SaveDetail([FromBody] AbReceiptDetailRequest r)
    {
        if (r.Receipt_Id == null) return BadRequest(ApiResponse<string>.Fail("Receipt_Id is required"));
        // Tenant: trước đây không kiểm tra đơn vị — thêm/sửa được dòng vào đơn nhận của đơn vị khác, gắn biểu ghi bất kỳ.
        var callerTenantId = GetTenantId();
        var receipt = await GetReceiptAsync(r.Receipt_Id);
        if (receipt == null || (callerTenantId.HasValue && receipt.TenantId != callerTenantId))
            return NotFound(ApiResponse<string>.Fail("Không tìm thấy đơn nhận"));
        if (IsLocked(receipt)) return LockedResult();
        if (r.Bibid.HasValue && !await _db.Bibs.AnyAsync(b => b.Bibid == r.Bibid && b.IsDelete != 2
                                                           && (b.TenantId == receipt.TenantId || b.TenantId == null)))
            return BadRequest(ApiResponse<string>.Fail("Biểu ghi không tồn tại hoặc thuộc đơn vị khác"));

        AbReceiptDetail entity;
        var existing = await _db.AbReceiptDetails
            .FirstOrDefaultAsync(x => x.Receipt_Id == r.Receipt_Id && x.Bibid == r.Bibid && x.IsDelete != 2);
        var userId = GetCurrentUserId();
        bool isNew = existing == null;
        if (existing != null)
        {
            entity = existing;
            PropertyMapper.Map(r, entity);
        }
        else
        {
            entity = new AbReceiptDetail { PublicId = Guid.NewGuid() };
            PropertyMapper.Map(r, entity);
            _db.AbReceiptDetails.Add(entity);
        }
        // PropertyMapper.Map bỏ qua "Bibid" (nằm trong danh sách audit-field dùng chung cho các
        // entity khác coi BibId là khóa hệ thống) — với AbReceiptDetail thì Bibid là dữ liệu
        // nghiệp vụ bắt buộc nên phải gán tay.
        entity.Bibid          = r.Bibid;
        entity.UpdateRowBy    = userId;
        entity.UpdatedRowDate = DateTime.Now;
        if (isNew)
        {
            entity.TenantId       = receipt.TenantId;
            entity.CreatedRowBy   = userId;
            entity.CreatedRowDate = DateTime.Now;
        }
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<AbReceiptDetail>.Ok(entity));
    }

    [HttpGet("Barcodes/{receiptId:long}/{bibId:long}")]
    [Permission("AB_RECEIPTS", "view")]
    public async Task<IActionResult> Barcodes(long receiptId, long bibId)
    {
        var tenantId = GetTenantId();
        var receiptExists = await _db.AbReceipts.AnyAsync(x => x.Id == receiptId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (!receiptExists) return NotFound(ApiResponse<string>.Fail("Không tìm thấy đơn nhận"));

        var barcodes = await _db.Barcodes
            .Where(x => x.Receipt_Id == receiptId && x.BibId == bibId && x.IsDelete != 2)
            .OrderBy(x => x.BarcodeValue)
            .ToListAsync();
        return Ok(ApiResponse<List<Barcode>>.Ok(barcodes));
    }

    // Lấy danh sách KCB theo ID dòng phiếu nhập (AbReceiptDetail.Id)
    [HttpGet("BarcodesByLine/{receiptLineId:long}")]
    [Permission("AB_RECEIPTS", "view")]
    public async Task<IActionResult> BarcodesByLine(long receiptLineId)
    {
        var line = await _db.AbReceiptDetails
            .FirstOrDefaultAsync(x => x.Id == receiptLineId && x.IsDelete != 2);
        if (line == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy dòng phiếu nhập"));
        var tenantId = GetTenantId();
        var receiptExists = await _db.AbReceipts.AnyAsync(x => x.Id == line.Receipt_Id && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (!receiptExists) return NotFound(ApiResponse<string>.Fail("Không tìm thấy dòng phiếu nhập"));

        var barcodes = await _db.Barcodes
            .Where(x => x.Receipt_Id == line.Receipt_Id && x.BibId == line.Bibid && x.IsDelete != 2)
            .OrderBy(x => x.BarcodeValue)
            .Select(x => new { x.Id, barcode = x.BarcodeValue, storeId = x.Store, x.Status, x.PublicId })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(barcodes));
    }

    // Đăng ký KCB theo lô cho 1 dòng phiếu nhập
    [HttpPost("RegisterBarcodes")]
    [Permission("AB_RECEIPTS", "edit")]
    public async Task<IActionResult> RegisterBarcodes([FromBody] RegisterBarcodesRequest r)
    {
        var line = await ScopedLines().FirstOrDefaultAsync(x => x.Id == r.ReceiptLineId);
        if (line == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy dòng phiếu nhập"));
        if (IsLocked(await GetReceiptAsync(line.Receipt_Id))) return LockedResult();
        if (r.Quantity <= 0) return BadRequest(ApiResponse<string>.Fail("Số lượng đăng ký phải lớn hơn 0"));

        // Port ELIB-LRC 09-22: chặn TRƯỚC khi ghi thay vì cảnh báo sau khi đã vượt số lượng trong đơn nhận.
        var existingCount = await CountLineBarcodesAsync(line);
        var allowed = line.Amount ?? int.MaxValue;
        if (existingCount + r.Quantity > allowed)
            return BadRequest(ApiResponse<string>.Fail($"Số lượng đăng ký vượt quá số lượng còn lại trong phiếu nhập (còn {Math.Max(0, allowed - existingCount)})"));

        // ĐKCB thuộc đơn vị của đơn nhận (kể cả khi tài khoản hệ thống thao tác). Mã ĐKCB duy nhất THEO ĐƠN VỊ (Đợt 20)
        // → số tiếp theo và kiểm tra trùng chỉ xét ĐKCB cùng đơn vị.
        var userId = GetUserId();
        var batch = await BarcodeNumbering.CreateBatchAsync(_db, line.TenantId, r.Prefix, r.DigitLength, r.Quantity, r.StartNumber, bc =>
        {
            bc.BibId        = line.Bibid;
            bc.Receipt_Id   = line.Receipt_Id;
            bc.Store        = r.StoreId;
            bc.CreatedRowBy = userId;
        });
        if (!batch.IsOk) return BadRequest(ApiResponse<string>.Fail(batch.Error!));
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { items = batch.Value!.Select(bc => new { id = bc.Id, barcode = bc.BarcodeValue, storeId = bc.Store }) }));
    }

    // Đăng ký 1 số KCB nhập tay (tab "Riêng lẻ") cho 1 dòng phiếu nhập
    [HttpPost("RegisterSingleBarcode")]
    [Permission("AB_RECEIPTS", "edit")]
    public async Task<IActionResult> RegisterSingleBarcode([FromBody] RegisterSingleBarcodeRequest r)
    {
        var line = await ScopedLines().FirstOrDefaultAsync(x => x.Id == r.ReceiptLineId);
        if (line == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy dòng phiếu nhập"));
        if (IsLocked(await GetReceiptAsync(line.Receipt_Id))) return LockedResult();

        var value = (r.BarcodeValue ?? "").Trim();
        if (string.IsNullOrEmpty(value)) return BadRequest(ApiResponse<string>.Fail("Số ĐKCB không được để trống"));

        var tenantId = line.TenantId;
        var existingCount = await CountLineBarcodesAsync(line);
        var allowed = line.Amount ?? int.MaxValue;
        if (existingCount + 1 > allowed)
            return BadRequest(ApiResponse<string>.Fail($"Đã đăng ký đủ số lượng trong phiếu nhập (còn {Math.Max(0, allowed - existingCount)})"));

        if (await BarcodeNumbering.ExistsAsync(_db, tenantId, value)) return BadRequest(ApiResponse<string>.Fail($"Số ĐKCB '{value}' đã tồn tại"));

        var userId   = GetUserId();
        var bc = new Barcode
        {
            BibId          = line.Bibid,
            Receipt_Id     = line.Receipt_Id,
            BarcodeValue   = value,
            Store          = r.StoreId,
            Status         = "I",
            PublicId       = Guid.NewGuid(),
            CreatedRowBy   = userId,
            CreatedRowDate = DateTime.Now,
            TenantId       = tenantId
        };
        _db.Barcodes.Add(bc);
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { id = bc.Id, barcode = bc.BarcodeValue, storeId = bc.Store, status = bc.Status, publicId = bc.PublicId }));
    }

    // Xóa 1 số KCB đã đăng ký
    [HttpDelete("DeleteBarcode/{barcodeId:long}")]
    [Permission("AB_RECEIPTS", "delete")]
    public async Task<IActionResult> DeleteBarcode(long barcodeId)
    {
        var tenantId = GetTenantId();
        var bc = await _db.Barcodes.FirstOrDefaultAsync(x => x.Id == barcodeId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (bc == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy số ĐKCB"));
        if (IsLocked(await GetReceiptAsync(bc.Receipt_Id))) return LockedResult();

        bc.IsDelete       = 2;
        bc.UpdateRowBy    = GetUserId();
        bc.UpdatedRowDate = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Đã xóa số ĐKCB"));
    }

    // Xóa dòng phiếu nhập (từ chối nếu còn KCB)
    [HttpDelete("DeleteLine/{lineId:long}")]
    [Permission("AB_RECEIPTS", "delete")]
    public async Task<IActionResult> DeleteLine(long lineId)
    {
        var line = await ScopedLines().FirstOrDefaultAsync(x => x.Id == lineId);
        if (line == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy dòng phiếu nhập"));
        if (IsLocked(await GetReceiptAsync(line.Receipt_Id))) return LockedResult();

        var hasBarcodes = await _db.Barcodes
            .AnyAsync(x => x.Receipt_Id == line.Receipt_Id && x.BibId == line.Bibid && x.IsDelete != 2);
        if (hasBarcodes)
        {
            return BadRequest(ApiResponse<string>.Fail(
                "Không thể xóa vì biểu ghi còn số KCB. Vui lòng xóa số KCB trước."));
        }

        line.IsDelete       = 2;
        line.UpdateRowBy    = GetUserId();
        line.UpdatedRowDate = DateTime.Now;
        await _db.SaveChangesAsync();

        // Nếu Bibid này không còn được dòng nào khác (ở phiếu nhập bất kỳ) tham chiếu nữa,
        // xóa luôn biểu ghi và dữ liệu liên quan — chỉ vừa xóa ở phiếu hiện tại nếu vẫn còn nơi khác dùng.
        var stillReferenced = line.Bibid.HasValue && await _db.AbReceiptDetails
            .AnyAsync(x => x.Bibid == line.Bibid && x.Id != line.Id && x.IsDelete != 2);

        if (line.Bibid.HasValue && !stillReferenced)
        {
            var bibId = line.Bibid.Value;
            var uid   = GetUserId();
            var now   = DateTime.Now;

            var bib = await _db.Bibs.FirstOrDefaultAsync(x => x.Bibid == bibId && x.IsDelete != 2);
            if (bib != null) { bib.IsDelete = 2; bib.UpdateRowBy = uid; bib.UpdatedRowDate = now; }

            var bibXml = await _db.BibXmls.FirstOrDefaultAsync(x => x.BibId == bibId && x.IsDelete != 2);
            if (bibXml != null) { bibXml.IsDelete = 2; bibXml.UpdateRowBy = uid; bibXml.UpdatedRowDate = now; }

            var bibDatas = await _db.BibDatas.Where(x => x.BibId == bibId && x.IsDelete != 2).ToListAsync();
            foreach (var bd in bibDatas) { bd.IsDelete = 2; bd.UpdateRowBy = uid; bd.UpdatedRowDate = now; }

            var barcodes = await _db.Barcodes.Where(x => x.BibId == bibId && x.IsDelete != 2).ToListAsync();
            foreach (var bc in barcodes) { bc.IsDelete = 2; bc.UpdateRowBy = uid; bc.UpdatedRowDate = now; }

            await _db.SaveChangesAsync();
        }

        return Ok(ApiResponse<string>.Ok("Đã xóa dòng phiếu nhập"));
    }

    // ── Tra cứu đơn nhận (theo từng dòng chi tiết, nhiều bộ lọc) ─────────────────

    private IQueryable<ReceiptLookupRow> BuildLookupQuery(ReceiptLookupRequest r)
    {
        var tenantId = GetTenantId();
        var q = from d in _db.AbReceiptDetails.Where(x => x.IsDelete != 2)
                join rc in _db.AbReceipts.Where(x => x.IsDelete != 2
                    && (!tenantId.HasValue || x.TenantId == tenantId)) on d.Receipt_Id equals rc.Id
                join b in _db.Bibs.Where(x => x.IsDelete != 2) on d.Bibid equals b.Bibid
                join x in _db.BibXmls on b.Bibid equals x.BibId into xs
                from x in xs.DefaultIfEmpty()
                select new ReceiptLookupRow { D = d, Rc = rc, B = b, X = x };

        if (r.ReceiptCodeFrom.HasValue) q = q.Where(t => t.Rc.Code >= r.ReceiptCodeFrom);
        if (r.ReceiptCodeTo.HasValue)   q = q.Where(t => t.Rc.Code <= r.ReceiptCodeTo);
        if (r.MfnFrom.HasValue)         q = q.Where(t => t.B.Mfn >= r.MfnFrom);
        if (r.MfnTo.HasValue)           q = q.Where(t => t.B.Mfn <= r.MfnTo);
        if (r.ReceiptDateFrom.HasValue) q = q.Where(t => t.Rc.Receipt_Date >= r.ReceiptDateFrom);
        if (r.ReceiptDateTo.HasValue)   q = q.Where(t => t.Rc.Receipt_Date <= r.ReceiptDateTo);
        if (r.CreatedDateFrom.HasValue) q = q.Where(t => t.Rc.CreatedDate >= r.CreatedDateFrom);
        if (r.CreatedDateTo.HasValue)   q = q.Where(t => t.Rc.CreatedDate <= r.CreatedDateTo);
        if (!string.IsNullOrEmpty(r.ReceiptName)) q = q.Where(t => t.Rc.Receipt_Name!.Contains(r.ReceiptName));
        if (r.Status.HasValue)     q = q.Where(t => t.Rc.Status == r.Status);
        if (r.SourceId.HasValue)   q = q.Where(t => t.Rc.Source_Id == r.SourceId);
        if (r.FundId.HasValue)     q = q.Where(t => t.Rc.FundId == r.FundId);
        if (r.SupplierId.HasValue) q = q.Where(t => t.Rc.Supplier_Id == r.SupplierId);
        if (r.StoreId.HasValue)    q = q.Where(t => t.Rc.Store_Id == r.StoreId);
        if (r.CreatedBy.HasValue)  q = q.Where(t => t.Rc.CreatedRowBy == r.CreatedBy);
        if (!string.IsNullOrEmpty(r.Title))       q = q.Where(t => t.X != null && t.X.Title!.Contains(r.Title));
        if (!string.IsNullOrEmpty(r.Author))      q = q.Where(t => t.X != null && t.X.Author!.Contains(r.Author));
        if (!string.IsNullOrEmpty(r.Publisher))   q = q.Where(t => t.X != null && t.X.Publisher!.Contains(r.Publisher));
        if (!string.IsNullOrEmpty(r.PublishYear)) q = q.Where(t => t.X != null && t.X.PublishDate!.Contains(r.PublishYear));

        return q;
    }

    private static string BuildIsbd(string? title, string? author, string? publisher, string? publishDate, string? page)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(title)) parts.Add(title);
        if (!string.IsNullOrWhiteSpace(author)) parts.Add("/ " + author);
        var pub = new List<string>();
        if (!string.IsNullOrWhiteSpace(publisher)) pub.Add(publisher);
        if (!string.IsNullOrWhiteSpace(publishDate)) pub.Add(publishDate);
        if (pub.Count > 0) parts.Add(".- " + string.Join(", ", pub));
        if (!string.IsNullOrWhiteSpace(page)) parts.Add(".- " + page);
        return string.Join(" ", parts);
    }

    [HttpPost("LookupLines")]
    [Permission("AB_RECEIPTS", "view")]
    public async Task<IActionResult> LookupLines([FromBody] ReceiptLookupRequest r)
    {
        var query = BuildLookupQuery(r).OrderByDescending(t => t.Rc.CreatedDate).ThenByDescending(t => t.D.Id);

        var totalCount = await query.CountAsync();
        var pageIndex  = r.PageIndex < 1 ? 1 : r.PageIndex;
        var pageSize   = r.PageSize  < 1 ? 20 : r.PageSize;
        var page = await query.Skip((Math.Max(pageIndex, 1) - 1) * pageSize).Take(pageSize).ToListAsync();

        var sourceMap = await _db.AbSources.ToDictionaryAsync(x => x.Id, x => x.Name);
        var storeMap  = await _db.Stores.ToDictionaryAsync(x => x.Id, x => x.Name);

        var items = page.Select(t => new
        {
            mfn         = t.B.Mfn,
            receiptCode = t.Rc.Code,
            isbd        = BuildIsbd(t.X?.Title, t.X?.Author, t.X?.Publisher, t.X?.PublishDate, t.X?.Page),
            createdDate = t.Rc.CreatedDate,
            sourceName  = t.Rc.Source_Id.HasValue && sourceMap.TryGetValue((int)t.Rc.Source_Id.Value, out var sn) ? sn : null,
            storeName   = t.Rc.Store_Id.HasValue && storeMap.TryGetValue(t.Rc.Store_Id.Value, out var stn) ? stn : null,
            bibPublicId = t.B.PublicId,
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new { items, totalCount, pageIndex, pageSize }));
    }

    [HttpPost("LookupLinesExport")]
    [Permission("AB_RECEIPTS", "view")]
    public async Task<IActionResult> LookupLinesExport([FromBody] ReceiptLookupRequest r, [FromServices] ISystemParameterService sysParam)
    {
        var query = BuildLookupQuery(r).OrderByDescending(t => t.Rc.CreatedDate).ThenByDescending(t => t.D.Id);
        var rows = await query.Take(20000).ToListAsync();

        var sourceMap = await _db.AbSources.ToDictionaryAsync(x => x.Id, x => x.Name);
        var storeMap  = await _db.Stores.ToDictionaryAsync(x => x.Id, x => x.Name);

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Tra cứu đơn nhận");
        string[] headers = ["Mfn", "Mã đơn", "Isbd", "Ngày tạo", "Nguồn", "Kho"];
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "TRA CỨU ĐƠN NHẬN TÀI LIỆU", headers.Length);
        for (var i = 0; i < headers.Length; i++) ws.Cell(startRow, i + 1).Value = headers[i];

        var row = startRow + 1;
        foreach (var t in rows)
        {
            ws.Cell(row, 1).Value = t.B.Mfn?.ToString() ?? "";
            ws.Cell(row, 2).Value = t.Rc.Code?.ToString() ?? "";
            ws.Cell(row, 3).Value = BuildIsbd(t.X?.Title, t.X?.Author, t.X?.Publisher, t.X?.PublishDate, t.X?.Page);
            ws.Cell(row, 4).Value = t.Rc.CreatedDate?.ToString("dd/MM/yyyy") ?? "";
            ws.Cell(row, 5).Value = t.Rc.Source_Id.HasValue && sourceMap.TryGetValue((int)t.Rc.Source_Id.Value, out var sn) ? sn : "";
            ws.Cell(row, 6).Value = t.Rc.Store_Id.HasValue && storeMap.TryGetValue(t.Rc.Store_Id.Value, out var stn) ? stn : "";
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, headers.Length);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "tra-cuu-don-nhan.xlsx");
    }

    [HttpPost("LookupLinesForLabel")]
    [Permission("AB_RECEIPTS", "view")]
    public async Task<IActionResult> LookupLinesForLabel([FromBody] ReceiptLookupRequest r)
    {
        var query = BuildLookupQuery(r).Take(5000);
        var items = await query
            .GroupBy(t => t.B.PublicId)
            .Select(g => new { bibPublicId = g.Key, amount = g.Sum(x => x.D.Amount ?? 0) })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(items));
    }
}

public class ReceiptLookupRow
{
    public AbReceiptDetail D  { get; set; } = null!;
    public AbReceipt       Rc { get; set; } = null!;
    public Bib             B  { get; set; } = null!;
    public BibXml?         X  { get; set; }
}

public class ReceiptLookupRequest
{
    public long?     ReceiptCodeFrom { get; set; }
    public long?     ReceiptCodeTo   { get; set; }
    public long?     MfnFrom         { get; set; }
    public long?     MfnTo           { get; set; }
    public DateTime? ReceiptDateFrom { get; set; }
    public DateTime? ReceiptDateTo   { get; set; }
    public DateTime? CreatedDateFrom { get; set; }
    public DateTime? CreatedDateTo   { get; set; }
    public string?   ReceiptName     { get; set; }
    public int?      Status          { get; set; }
    public long?     SourceId        { get; set; }
    public long?     FundId          { get; set; }
    public long?     SupplierId      { get; set; }
    public long?     StoreId         { get; set; }
    public long?     CreatedBy       { get; set; }
    public string?   Title           { get; set; }
    public string?   Author          { get; set; }
    public string?   Publisher       { get; set; }
    public string?   PublishYear     { get; set; }
    public int       PageIndex       { get; set; } = 1;
    public int       PageSize        { get; set; } = 20;
}

public class RegisterBarcodesRequest
{
    public long    ReceiptLineId { get; set; }
    public string? Prefix        { get; set; }
    public int     DigitLength   { get; set; } = 6;
    public int     Quantity      { get; set; }
    public int?    StoreId       { get; set; }
    public int?    StartNumber   { get; set; }
}

public class RegisterSingleBarcodeRequest
{
    public long    ReceiptLineId { get; set; }
    public string? BarcodeValue  { get; set; }
    public int?    StoreId       { get; set; }
}
