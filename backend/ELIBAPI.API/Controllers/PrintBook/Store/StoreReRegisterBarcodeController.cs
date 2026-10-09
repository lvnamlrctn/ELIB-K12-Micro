using System.Security.Claims;
using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Store/ReRegisterBarcode")]
[Authorize]
public class StoreReRegisterBarcodeController(ELIBAPIDbContext db, BarcodeReRegisterService barcodeReRegister) : ControllerBase
{
    private long? GetTenantId()
    {
        var claim = User.FindFirstValue("TenantId");
        return long.TryParse(claim, out var id) ? id : null;
    }

    [HttpGet("Lookup/{barcode}")]
    [Permission("RE_REGISTER", "view")]
    public async Task<IActionResult> Lookup(string barcode)
    {
        var tenantId = GetTenantId();
        var (bc, bcAmbiguous) = await BarcodeTenantLookup.ByValueAsync(db.Barcodes, barcode ?? "", tenantId);
        if (bcAmbiguous) return BadRequest(ApiResponse<string>.Fail(BarcodeTenantLookup.AmbiguousMessage));
        if (bc == null) return NotFound(ApiResponse<string>.Fail("Mã vạch không tồn tại"));

        string? title = null, author = null;
        if (bc.BibId.HasValue)
        {
            var xml = await db.BibXmls.FirstOrDefaultAsync(x => x.BibId == bc.BibId.Value);
            title  = xml?.Title;
            author = xml?.Author;
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            bc.Id,
            bc.BarcodeValue,
            Store  = bc.Store,
            bc.Status,
            bc.BibId,
            Title  = title,
            Author = author
        }));
    }

    [HttpPost("ReRegister")]
    [Permission("RE_REGISTER", "edit")]
    public async Task<IActionResult> ReRegister([FromBody] ReRegisterRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.OldBarcode) || string.IsNullOrWhiteSpace(r.NewBarcode))
            return BadRequest(ApiResponse<string>.Fail("OldBarcode và NewBarcode là bắt buộc"));

        var tenantId = GetTenantId();
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userId = long.TryParse(claim, out var uid) ? uid : (long?)null;
        var actorName = userId.HasValue
            ? await db.Users.AsNoTracking().Where(u => u.Id == userId.Value).Select(u => u.FullName ?? u.LoginName).FirstOrDefaultAsync()
            : null;

        await using var tx = await db.Database.BeginTransactionAsync();
        var outcome = await barcodeReRegister.ExecuteAsync(tenantId, r.OldBarcode!, r.NewBarcode!, r.StoreId, userId, actorName,
            HttpContext.Connection.RemoteIpAddress?.ToString(), previewOnly: false);
        if (!outcome.Success)
        {
            await tx.RollbackAsync();
            return outcome.Error == "Mã vạch cũ không tồn tại"
                ? NotFound(ApiResponse<string>.Fail(outcome.Error))
                : BadRequest(ApiResponse<string>.Fail(outcome.Error ?? "Không thể đánh lại mã"));
        }
        await tx.CommitAsync();
        return Ok(ApiResponse<object>.Ok(new { oldBarcode = r.OldBarcode, newBarcode = r.NewBarcode, updated = outcome.Updated }));
    }

    /// <summary>Đánh lại mã ĐKCB hàng loạt từ file Excel (Đợt 22.5, cùng khuôn <c>ReaderController.Import</c>):
    /// <paramref name="background"/>=false → chạy ngay, tối đa <see cref="SyncMaxRows"/> dòng; =true → tạo tác vụ
    /// nền xem trước (chunk 200 dòng/lần qua <see cref="AdminTaskService"/>, đúng logic đơn lẻ ở trên nhờ dùng
    /// chung <see cref="BarcodeReRegisterService"/>).</summary>
    private const int SyncMaxRows = 1000;

    [HttpPost("Import")]
    [Permission("RE_REGISTER", "edit")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Import(IFormFile file, [FromForm] bool background = false,
        [FromServices] AdminTaskService? tasks = null, [FromServices] IConfiguration? config = null)
    {
        if (file == null || file.Length == 0) return BadRequest(ApiResponse<object>.Fail("Không tìm thấy file", 400));

        List<BarcodeReRegisterRow> rows;
        try
        {
            using var stream = file.OpenReadStream();
            rows = BarcodeReRegisterImportWorkbook.Read(stream);
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message, 400)); }
        catch (Exception) { return BadRequest(ApiResponse<object>.Fail("File Excel bị lỗi hoặc không đúng định dạng.", 400)); }

        // Port ELIB-LRC 10-04: cùng 1 mã cũ (hoặc mã mới) lặp lại trong tệp — lần 2 đổi tiếp bản đã đổi / đè mã vừa cấp. Từ
        // chối cả tệp, áp cho cả đường đồng bộ lẫn tác vụ nền (chạy theo lô 200 dòng nên không tự phát hiện được giữa các lô).
        static IEnumerable<string> Repeated(IEnumerable<string?> codes) => codes.Where(c => !string.IsNullOrWhiteSpace(c))
            .GroupBy(c => c!.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key);
        var repeated = Repeated(rows.Select(r => r.OldBarcode)).Select(c => $"mã cũ {c}")
            .Concat(Repeated(rows.Select(r => r.NewBarcode)).Select(c => $"mã mới {c}")).Take(20).ToList();
        if (repeated.Count > 0)
            return BadRequest(ApiResponse<object>.Fail("Tệp có mã lặp lại: " + string.Join(", ", repeated), 400));

        var tenantId = GetTenantId();
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = long.TryParse(claim, out var uid) ? uid : (long?)null;

        if (background)
        {
            if (config == null || !config.GetValue<bool>("AdminTasks:Enabled"))
                return BadRequest(ApiResponse<object>.Fail("Xử lý nền chưa được bật trên hệ thống.", 400));
            try
            {
                var task = await tasks!.Enqueue(userId ?? 0, tenantId,
                    new AdminTaskRequest { Kind = "barcode-reregister-import", Preview = true, BarcodeRows = rows });
                return Accepted(ApiResponse<object>.Ok(AdminTaskService.View(task), "Đã tạo tác vụ xem trước."));
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message, 400)); }
        }

        if (rows.Count > SyncMaxRows)
            return BadRequest(ApiResponse<object>.Fail($"Vượt giới hạn {SyncMaxRows} dòng cho đường đồng bộ — bật xử lý nền để nhập nhiều hơn.", 400));

        var actorName = userId.HasValue
            ? await db.Users.AsNoTracking().Where(u => u.Id == userId.Value).Select(u => u.FullName ?? u.LoginName).FirstOrDefaultAsync()
            : null;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var errors = new List<string>();
        int success = 0, failed = 0;
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.OldBarcode) || string.IsNullOrWhiteSpace(row.NewBarcode))
            {
                failed++; errors.Add("Dòng thiếu mã vạch cũ/mã vạch mới"); continue;
            }
            await using var tx = await db.Database.BeginTransactionAsync();
            var outcome = await barcodeReRegister.ExecuteAsync(tenantId, row.OldBarcode!, row.NewBarcode!, row.StoreId, userId, actorName, ip, previewOnly: false);
            if (!outcome.Success)
            {
                await tx.RollbackAsync();
                failed++; errors.Add($"{row.OldBarcode} → {row.NewBarcode}: {outcome.Error}");
                continue;
            }
            await tx.CommitAsync();
            success++;
        }
        return Ok(ApiResponse<object>.Ok(new { successCount = success, failedCount = failed, errors },
            $"Đánh lại mã hoàn tất: {success} thành công, {failed} lỗi"));
    }
}

public class ReRegisterRequest
{
    public string? OldBarcode { get; set; }
    public string? NewBarcode { get; set; }
    public long?   StoreId    { get; set; }
}
