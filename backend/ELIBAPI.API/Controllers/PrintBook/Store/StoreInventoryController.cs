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

[Route("api/PrintBook/Store/Inventory")]
public class StoreInventoryController : GenericController<Inventory, InventorySearchRequest, InventoryRequest>
{
    private readonly ELIBAPIDbContext _db;
    private readonly InventoryImportService _inventoryImport;

    public StoreInventoryController(
        IGenericRepository<Inventory, InventorySearchRequest, InventoryRequest> repo,
        ELIBAPIDbContext db, InventoryImportService inventoryImport) : base(repo)
    {
        _db = db;
        _inventoryImport = inventoryImport;
    }

    [HttpPost("Add")] [Permission("INVENTORY", "add")]
    public override async Task<IActionResult> Add([FromBody] InventoryRequest r) => await base.Add(r);

    [HttpPut("Update/{publicId:guid}")] [Permission("INVENTORY", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] InventoryRequest r) => await base.Update(publicId, r);

    [HttpDelete("Delete/{publicId:guid}")] [Permission("INVENTORY", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")] [Permission("INVENTORY", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest r) => await base.ChangeStatus(r);

    [HttpGet("{id:long}")] [Permission("INVENTORY", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")] [Permission("INVENTORY", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")] [Permission("INVENTORY", "view")]
    public override async Task<IActionResult> Search([FromBody] InventorySearchRequest r) => await base.Search(r);

    [HttpPost("SearchAll")] [Permission("INVENTORY", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] InventorySearchRequest r) => await base.SearchAll(r);

    [HttpPost("ScanBarcode")]
    [Permission("INVENTORY", "add")]
    public async Task<IActionResult> ScanBarcode([FromBody] ScanBarcodeRequest r)
    {
        // Đợt 20: phiên kiểm kê phải thuộc đơn vị người gọi; mã ĐKCB chỉ duy nhất trong 1 đơn vị → tra bản sách trong
        // đúng đơn vị của phiên kiểm kê (kể cả khi tài khoản hệ thống quét).
        var (tenantId, found) = await _inventoryImport.ResolveTenantAsync(r.InventoryId ?? 0, GetTenantId());
        if (!found) return NotFound(ApiResponse<string>.Fail("Không tìm thấy phiên kiểm kê"));

        // Port ELIB-LRC 10-04: mã không có trong CSDL vẫn được ghi nhận "chưa đăng ký" (Unregistered) thay vì báo lỗi;
        // đợt đã kết thúc trả 400.
        var outcome = await _inventoryImport.ScanAsync(r.InventoryId ?? 0, tenantId, r.Barcode ?? "", r.StoreId, GetCurrentUserId(), previewOnly: false);
        if (!outcome.Success) return BadRequest(ApiResponse<string>.Fail(outcome.Error ?? "Mã vạch không hợp lệ"));
        var row = (await _inventoryImport.ToRowsAsync([outcome.Entity!]))[0];
        return Ok(ApiResponse<InventoryScanRow>.Ok(row, outcome.Unregistered ? "Mã vạch chưa đăng ký trong CSDL — đã ghi nhận" : "Success."));
    }

    /// <summary>Nhập danh sách mã đã quét hàng loạt từ file Excel (Đợt 22.5, cùng khuôn
    /// <c>ReaderController.Import</c>/<c>StoreReRegisterBarcodeController.Import</c>): <paramref name="background"/>=false
    /// → chạy ngay, tối đa <see cref="SyncMaxRows"/> dòng; =true → tác vụ nền xem trước. Cùng logic quét tay
    /// (<see cref="ScanBarcode"/>) nhờ dùng chung <see cref="InventoryImportService"/>.</summary>
    private const int SyncMaxRows = 2000;

    [HttpPost("Import")]
    [Permission("INVENTORY", "add")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Import(IFormFile file, [FromForm] long inventoryId, [FromForm] bool background = false,
        [FromServices] AdminTaskService? tasks = null, [FromServices] Microsoft.Extensions.Configuration.IConfiguration? config = null)
    {
        if (file == null || file.Length == 0) return BadRequest(ApiResponse<object>.Fail("Không tìm thấy file", 400));
        if (await _inventoryImport.IsClosedAsync(inventoryId))
            return BadRequest(ApiResponse<object>.Fail("Đợt kiểm kê đã kết thúc, không nhận thêm mã vạch", 400));

        List<InventoryImportRow> rows;
        try
        {
            using var stream = file.OpenReadStream();
            rows = InventoryImportWorkbook.Read(stream);
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message, 400)); }
        catch (Exception) { return BadRequest(ApiResponse<object>.Fail("File Excel bị lỗi hoặc không đúng định dạng.", 400)); }

        var callerTenantId = GetTenantId();
        var userId = GetCurrentUserId();

        if (background)
        {
            if (config == null || !config.GetValue<bool>("AdminTasks:Enabled"))
                return BadRequest(ApiResponse<object>.Fail("Xử lý nền chưa được bật trên hệ thống.", 400));
            try
            {
                var task = await tasks!.Enqueue(userId ?? 0, callerTenantId,
                    new AdminTaskRequest { Kind = "inventory-import", Preview = true, InventoryId = inventoryId, InventoryRows = rows });
                return Accepted(ApiResponse<object>.Ok(AdminTaskService.View(task), "Đã tạo tác vụ xem trước."));
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message, 400)); }
        }

        if (rows.Count > SyncMaxRows)
            return BadRequest(ApiResponse<object>.Fail($"Vượt giới hạn {SyncMaxRows} dòng cho đường đồng bộ — bật xử lý nền để nhập nhiều hơn.", 400));

        var (tenantId, found) = await _inventoryImport.ResolveTenantAsync(inventoryId, callerTenantId);
        if (!found) return NotFound(ApiResponse<string>.Fail("Không tìm thấy phiên kiểm kê"));

        var errors = new List<string>();
        int success = 0, skipped = 0, failed = 0;
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Barcode)) { failed++; errors.Add("Dòng thiếu mã vạch"); continue; }
            var outcome = await _inventoryImport.ScanAsync(inventoryId, tenantId, row.Barcode!, row.StoreId, userId, previewOnly: false);
            if (!outcome.Success) { failed++; errors.Add($"{row.Barcode}: {outcome.Error}"); continue; }
            if (outcome.AlreadyScanned) skipped++; else success++;
        }
        return Ok(ApiResponse<object>.Ok(new { successCount = success, skippedCount = skipped, failedCount = failed, errors },
            $"Nhập kiểm kê hoàn tất: {success} thành công, {skipped} đã quét trước đó, {failed} lỗi"));
    }

    // Bộ lọc ≤ 0 (frontend gửi -1 = "Tất cả") là không lọc; trả kèm nhan đề và tên kho (port ELIB-LRC 10-04).
    [HttpPost("SearchBarcode")]
    [Permission("INVENTORY", "view")]
    public async Task<IActionResult> SearchBarcode([FromBody] InventoryScanSearchRequest r)
    {
        var (_, found) = await _inventoryImport.ResolveTenantAsync(r.InventoryId ?? 0, GetTenantId());
        if (!found) return NotFound(ApiResponse<string>.Fail("Không tìm thấy phiên kiểm kê"));
        var (items, total) = await _inventoryImport.SearchScannedAsync(r.InventoryId ?? 0, r.CheckStoreStatus, r.CheckBorrow, r.CheckStatus, r.PageIndex, r.PageSize);
        return Ok(ApiResponse<object>.Ok(new { items, totalCount = total }));
    }

    // Trước đây "Chưa đăng ký" và "Thiếu (mất)" luôn bằng 0 vì backend không trả 2 số này.
    [HttpGet("Summary/{inventoryId:long}")]
    [Permission("INVENTORY", "view")]
    public async Task<IActionResult> Summary(long inventoryId)
    {
        var (tenantId, found) = await _inventoryImport.ResolveTenantAsync(inventoryId, GetTenantId());
        if (!found) return NotFound(ApiResponse<string>.Fail("Không tìm thấy phiên kiểm kê"));
        var s = await _inventoryImport.SummaryAsync(inventoryId, tenantId);
        return Ok(ApiResponse<object>.Ok(new
        {
            scanned = s.Scanned, notCorrectStore = s.NotCorrectStore, notRegister = s.NotRegister,
            onLoan = s.OnLoan, notBorrowed = s.OnLoan, damaged = s.Damaged, lost = s.Lost
        }));
    }

    // "Nghi mất": chỉ các kho có bản đã quét trong đợt, không tính bản đang mượn / đã ghi nhận mất / đã thanh lý; chạy
    // trong CSDL. Trước đây tải mọi ĐKCB của đơn vị vào bộ nhớ và coi mọi bản chưa quét là nghi mất.
    [HttpPost("ProcessLost")]
    [Permission("INVENTORY", "edit")]
    public async Task<IActionResult> ProcessLost([FromBody] ProcessLostRequest r)
    {
        var (tenantId, found) = await _inventoryImport.ResolveTenantAsync(r.InventoryId, GetTenantId());
        if (!found) return NotFound(ApiResponse<string>.Fail("Không tìm thấy phiên kiểm kê"));
        var items = await _inventoryImport.MissingAsync(r.InventoryId, tenantId);
        return Ok(ApiResponse<object>.Ok(new { count = items.Count, items }));
    }

    [HttpGet("Report/{inventoryId:long}")]
    [Permission("INVENTORY", "view")]
    public async Task<IActionResult> Report(long inventoryId, [FromServices] ISystemParameterService sysParam)
    {
        var (tenantId, found) = await _inventoryImport.ResolveTenantAsync(inventoryId, GetTenantId());
        if (!found) return NotFound(ApiResponse<string>.Fail("Không tìm thấy phiên kiểm kê"));

        var tally        = await _inventoryImport.SummaryAsync(inventoryId, tenantId);
        var scannedItems = await _inventoryImport.ScannedAsync(inventoryId);
        var unscanned    = await _inventoryImport.MissingAsync(inventoryId, tenantId);

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);

        var wsSummary = wb.Worksheets.Add("Tổng quan");
        var row = ExcelReportHelper.WriteLetterhead(wsSummary, parentLibrary, libraryName, "BÁO CÁO KIỂM KÊ - TỔNG QUAN", 2);
        var summaryStartRow = row;
        foreach (var (label, value) in new (string, object)[]
        {
            ("Chỉ số", "Số lượng"),
            ("Đã quét", tally.Scanned),
            ("Sai vị trí kho", tally.NotCorrectStore),
            ("Chưa đăng ký (mã không có trong CSDL)", tally.NotRegister),
            // Nhãn cũ "Chưa mượn nhưng không có trên kệ" sai nghĩa.
            ("Có trên kệ nhưng còn phiếu mượn mở", tally.OnLoan),
            ("Tình trạng khác bình thường", tally.Damaged),
            ("Chưa quét (nghi mất)", tally.Lost),
        })
        {
            wsSummary.Cell(row, 1).Value = label;
            wsSummary.Cell(row, 2).Value = value is int n ? n : value.ToString();
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(wsSummary, summaryStartRow, row - 1, 1, 2);

        var wsScanned = wb.Worksheets.Add("Chi tiết đã quét");
        var scannedStartRow = ExcelReportHelper.WriteLetterhead(wsScanned, parentLibrary, libraryName, "BÁO CÁO KIỂM KÊ - CHI TIẾT ĐÃ QUÉT", 6);
        string[] scannedHeader = ["Barcode", "Tên sách", "Kho", "Vị trí kho", "Tình trạng mượn", "Tình trạng vật lý"];
        for (var c = 0; c < scannedHeader.Length; c++) wsScanned.Cell(scannedStartRow, c + 1).Value = scannedHeader[c];
        row = scannedStartRow + 1;
        foreach (var x in scannedItems)
        {
            wsScanned.Cell(row, 1).Value = x.Barcode ?? "";
            wsScanned.Cell(row, 2).Value = x.CheckRegisteter == InventoryImportService.No ? "(chưa đăng ký)" : x.BibTitle ?? "";
            wsScanned.Cell(row, 3).Value = x.StoreName ?? "";
            wsScanned.Cell(row, 4).Value = x.CheckStoreStatus == InventoryImportService.No ? "Sai kho" : "Đúng kho";
            wsScanned.Cell(row, 5).Value = x.CheckBorrow == InventoryImportService.No ? "Đang mượn" : "Không mượn";
            wsScanned.Cell(row, 6).Value = x.CheckStatus == InventoryImportService.Yes ? "Bình thường" : "Khác thường";
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(wsScanned, scannedStartRow, row - 1, 1, scannedHeader.Length);

        var wsUnscanned = wb.Worksheets.Add("Chưa quét (nghi mất)");
        var unscannedStartRow = ExcelReportHelper.WriteLetterhead(wsUnscanned, parentLibrary, libraryName, "BÁO CÁO KIỂM KÊ - CHƯA QUÉT (NGHI MẤT)", 3);
        wsUnscanned.Cell(unscannedStartRow, 1).Value = "Barcode";
        wsUnscanned.Cell(unscannedStartRow, 2).Value = "Tên sách";
        wsUnscanned.Cell(unscannedStartRow, 3).Value = "Kho";
        row = unscannedStartRow + 1;
        foreach (var b in unscanned)
        {
            wsUnscanned.Cell(row, 1).Value = b.Barcode;
            wsUnscanned.Cell(row, 2).Value = b.BibTitle ?? "";
            wsUnscanned.Cell(row, 3).Value = b.StoreName ?? "";
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(wsUnscanned, unscannedStartRow, row - 1, 1, 3);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"inventory-{inventoryId}.xlsx");
    }

    private long? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(claim, out var id) ? id : null;
    }
}

public class ScanBarcodeRequest
{
    public long?   InventoryId { get; set; }
    public string? Barcode     { get; set; }
    public long?   StoreId     { get; set; }
}

public class InventoryScanSearchRequest
{
    public long? InventoryId      { get; set; }
    public int?  CheckStoreStatus { get; set; }
    public int?  CheckBorrow      { get; set; }
    public int?  CheckStatus      { get; set; }
    public int?  PageIndex        { get; set; }
    public int?  PageSize         { get; set; }
}

public class ProcessLostRequest { public long InventoryId { get; set; } }
