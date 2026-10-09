using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class CPhotoController : GenericController<CPhoto, CPhotoSearchRequest, CPhotoRequest>
{
    private readonly ELIBAPIDbContext _db;

    public CPhotoController(
        IGenericRepository<CPhoto, CPhotoSearchRequest, CPhotoRequest> repo,
        ELIBAPIDbContext db) : base(repo) => _db = db;

    private long? CurrentTenantId()
    {
        var claim = User.FindFirst("TenantId")?.Value;
        return long.TryParse(claim, out var id) ? id : null;
    }

    [HttpPost("Add")]
    [Permission("C_PHOTO", "add")]
    public override async Task<IActionResult> Add([FromBody] CPhotoRequest request)
    {
        var error = await ValidateAndNormalizeAsync(request);
        if (error != null) return BadRequest(ApiResponse<string>.Fail(error));
        return await base.Add(request);
    }

    [HttpPut("Update/{publicId:guid}")]
    [Permission("C_PHOTO", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] CPhotoRequest request)
    {
        var error = await ValidateAndNormalizeAsync(request);
        if (error != null) return BadRequest(ApiResponse<string>.Fail(error));
        return await base.Update(publicId, request);
    }

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("C_PHOTO", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("C_PHOTO", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("C_PHOTO", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("C_PHOTO", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("C_PHOTO", "view")]
    public override async Task<IActionResult> Search([FromBody] CPhotoSearchRequest request)
    {
        var result = await _repo.SearchAsync(request);
        var rows   = await BuildListRowsAsync(result.Items);
        return Ok(ApiResponse<PagedResult<object>>.Ok(new PagedResult<object>
        {
            Items      = rows,
            TotalCount = result.TotalCount,
            PageIndex  = result.PageIndex,
            PageSize   = result.PageSize
        }));
    }

    [HttpPost("SearchAll")]
    [Permission("C_PHOTO", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] CPhotoSearchRequest request) => await base.SearchAll(request);

    // Tổng thành tiền trên TOÀN BỘ kết quả khớp filter (không chỉ trang hiện tại).
    [HttpPost("Totals")]
    [Permission("C_PHOTO", "view")]
    public async Task<IActionResult> Totals([FromBody] CPhotoSearchRequest request)
    {
        var all = await _repo.SearchAllAsync(request);
        return Ok(ApiResponse<object>.Ok(new
        {
            TotalAmountSum = all.Sum(x => x.TotalAmount ?? 0),
            TotalPaid      = all.Where(x => x.IsPaid == 2).Sum(x => x.TotalAmount ?? 0),
            TotalUnpaid    = all.Where(x => x.IsPaid != 2).Sum(x => x.TotalAmount ?? 0),
            Count          = all.Count
        }));
    }

    // Tra cứu bạn đọc theo số thẻ — dùng cho ô "Số thẻ" trên form nhập phiếu photo.
    [HttpGet("LookupCard/{cardNo}")]
    [Permission("C_PHOTO", "view")]
    public async Task<IActionResult> LookupCard(string cardNo)
    {
        var reader = await FindReaderAsync(cardNo);
        if (reader == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy bạn đọc với số thẻ này", 404));

        var unitName = reader.ClassId.HasValue
            ? await _db.Classes.Where(x => x.Id == reader.ClassId).Select(x => x.Name).FirstOrDefaultAsync()
            : null;

        return Ok(ApiResponse<object>.Ok(new
        {
            ReaderId = reader.Id,
            CardNo   = reader.Cardno,
            FullName = $"{reader.FirstName} {reader.LastName}".Trim(),
            UnitName = unitName
        }));
    }

    // Tra cứu tài liệu theo Số ĐKCB — dùng cho ô "Số ĐKCB" trên form nhập phiếu photo.
    [HttpGet("LookupBarcode/{barcode}")]
    [Permission("C_PHOTO", "view")]
    public async Task<IActionResult> LookupBarcode(string barcode)
    {
        var bc = await FindBarcodeAsync(barcode);
        if (bc == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy Số ĐKCB này", 404));

        var title = bc.BibId.HasValue
            ? await _db.BibXmls.Where(x => x.BibId == bc.BibId).Select(x => x.Title).FirstOrDefaultAsync()
            : null;

        return Ok(ApiResponse<object>.Ok(new
        {
            Barcode  = bc.BarcodeValue,
            BibId    = bc.BibId,
            BibTitle = title
        }));
    }

    // Kiểm tra dữ liệu + resolve Reader_Id + tính lại thành tiền. Trả null nếu hợp lệ, ngược lại trả thông báo lỗi.
    private async Task<string?> ValidateAndNormalizeAsync(CPhotoRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.CardNo))  return "Vui lòng nhập số thẻ bạn đọc";
        if (string.IsNullOrWhiteSpace(r.Barcode)) return "Vui lòng nhập Số ĐKCB";

        var reader = await FindReaderAsync(r.CardNo);
        if (reader == null) return "Không tìm thấy bạn đọc với số thẻ này";
        r.Reader_Id = reader.Id;

        var bc = await FindBarcodeAsync(r.Barcode);
        if (bc == null) return "Không tìm thấy Số ĐKCB này";
        r.Barcode = bc.BarcodeValue;

        if (!r.Frompage.HasValue || r.Frompage < 1)   return "Trang bắt đầu phải lớn hơn hoặc bằng 1";
        if (!r.ToPage.HasValue || r.ToPage < r.Frompage) return "Trang kết thúc phải lớn hơn hoặc bằng trang bắt đầu";
        if (!r.Copy.HasValue || r.Copy < 1)           return "Số bản copy phải lớn hơn hoặc bằng 1";
        if (!r.Price.HasValue || r.Price < 0)         return "Đơn giá không hợp lệ";

        r.PhotoDate ??= DateTime.Now.Date;
        r.IsPaid = r.IsPaid == 2 ? 2 : 1;
        // Luôn tính lại ở server — không tin giá trị client gửi lên.
        r.TotalAmount = (r.ToPage.Value - r.Frompage.Value + 1) * r.Copy.Value * r.Price.Value;
        return null;
    }

    private Task<Reader?> FindReaderAsync(string cardNo)
    {
        var tenantId = CurrentTenantId();
        var key = cardNo.Trim().ToLower();
        return _db.Readers.FirstOrDefaultAsync(x =>
            x.Cardno!.ToLower() == key && x.IsDelete != 2 &&
            (!tenantId.HasValue || x.TenantId == tenantId));
    }

    // Mã ĐKCB chỉ duy nhất trong 1 đơn vị (Đợt 20): tài khoản hệ thống quét mã có ở nhiều đơn vị → coi như không tìm
    // thấy (không đoán bản của đơn vị nào).
    private async Task<Barcode?> FindBarcodeAsync(string barcode)
    {
        var (bc, _) = await BarcodeTenantLookup.ByValueAsync(_db.Barcodes, barcode, CurrentTenantId(), normalize: true);
        return bc;
    }

    // Bổ sung tên bạn đọc / đơn vị / nhan đề cho từng dòng kết quả — nạp theo lô, tránh N+1.
    private async Task<List<object>> BuildListRowsAsync(List<CPhoto> items)
    {
        var readerIds = items.Where(x => x.Reader_Id.HasValue).Select(x => x.Reader_Id!.Value).Distinct().ToList();
        var readers   = await _db.Readers.Where(x => readerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

        var classIds = readers.Values.Where(x => x.ClassId.HasValue).Select(x => x.ClassId!.Value).Distinct().ToList();
        var classes  = await _db.Classes.Where(x => classIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);

        var barcodeValues = items.Where(x => !string.IsNullOrEmpty(x.Barcode)).Select(x => x.Barcode!).Distinct().ToList();
        var barcodes = await _db.Barcodes.Where(x => barcodeValues.Contains(x.BarcodeValue!))
            .Select(x => new { x.BarcodeValue, x.TenantId, x.BibId })
            .ToListAsync();
        // Khoá (mã, đơn vị) — 2 đơn vị có thể cùng mã ĐKCB (Đợt 20).
        var bibIdByBarcode = barcodes.Where(x => x.BibId.HasValue)
            .GroupBy(x => (x.BarcodeValue!, x.TenantId ?? 0))
            .ToDictionary(g => g.Key, g => g.First().BibId!.Value);
        var bibIds    = bibIdByBarcode.Values.Distinct().ToList();
        var bibTitles = await _db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId, x => x.Title);

        return items.Select(t =>
        {
            Reader? reader = t.Reader_Id.HasValue && readers.TryGetValue(t.Reader_Id.Value, out var rd) ? rd : null;
            string? unitName = reader?.ClassId != null && classes.TryGetValue(reader.ClassId.Value, out var cn) ? cn : null;
            string? bibTitle = null;
            if (!string.IsNullOrEmpty(t.Barcode) && bibIdByBarcode.TryGetValue((t.Barcode, t.TenantId ?? 0), out var bibId))
                bibTitles.TryGetValue(bibId, out bibTitle);

            return (object)new
            {
                t.Id,
                t.PublicId,
                ReaderId   = t.Reader_Id,
                CardNo     = reader?.Cardno,
                ReaderName = reader != null ? $"{reader.FirstName} {reader.LastName}".Trim() : null,
                UnitName   = unitName,
                t.Barcode,
                BibTitle   = bibTitle,
                t.PhotoDate,
                t.Frompage,
                t.ToPage,
                Pages      = (t.ToPage ?? 0) - (t.Frompage ?? 0) + 1,
                t.Copy,
                t.Price,
                t.TotalAmount,
                t.IsPaid
            };
        }).ToList();
    }
}
