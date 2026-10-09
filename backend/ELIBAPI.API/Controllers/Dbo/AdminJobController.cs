using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Jobs;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers;

// Đợt 18: mọi thao tác POST ở đây chạy trên index Elasticsearch DÙNG CHUNG của mọi đơn vị → chỉ tài khoản
// hệ thống ([SystemAdminOnly]); quyền DIGITAL_DOC của admin 1 đơn vị không còn đủ.
[Route("api/admin/job")]
public class AdminJobController : BaseApiController
{
    [HttpPost("reindex-embedding")]
    [Permission("DIGITAL_DOC", "edit")]
    [SystemAdminOnly]
    public IActionResult TriggerEmbeddingReindex([FromServices] IBackgroundJobClient jobClient)
    {
        var jobId = jobClient.Enqueue<EmbeddingReindexJob>(x => x.RunAsync());
        return Ok(ApiResponse<object>.Ok(new { jobId }, "Job đã được thêm vào hàng đợi"));
    }

    [HttpGet("embedding-status")]
    [Permission("DIGITAL_DOC", "view")]
    public async Task<IActionResult> GetEmbeddingStatus([FromServices] IElasticsearchService elastic)
    {
        var (total, embedded, pending) = await elastic.GetEmbeddingStatusAsync();
        var percent = total > 0 ? Math.Round(embedded * 100.0 / total, 2) : 0;
        return Ok(ApiResponse<object>.Ok(new { total, embedded, pending, percent }));
    }

    // ── Index gộp library_docs ────────────────────────────────────────────────

    /// <summary>
    /// Bước 1: chuyển tài liệu số từ index chunk cũ sang index gộp bằng ES _reindex.
    /// KHÔNG tải lại PDF / OCR / sinh embedding — vector được sao chép nguyên.
    /// Chạy nền phía ES; theo dõi bằng GET task-status/{taskId}.
    /// </summary>
    [HttpPost("migrate-digital-to-unified")]
    [Permission("DIGITAL_DOC", "edit")]
    [SystemAdminOnly]
    public async Task<IActionResult> MigrateDigitalToUnified([FromServices] IElasticsearchService elastic)
    {
        var taskId = await elastic.MigrateDigitalToUnifiedAsync();
        return Ok(ApiResponse<object>.Ok(new { taskId },
            "Đã bắt đầu chuyển tài liệu số sang index gộp"));
    }

    [HttpGet("task-status/{taskId}")]
    [Permission("DIGITAL_DOC", "view")]
    public async Task<IActionResult> GetTaskStatus(string taskId, [FromServices] IElasticsearchService elastic)
        => Ok(ApiResponse<object>.Ok(await elastic.GetTaskStatusAsync(taskId)));

    /// <summary>
    /// Bước 2: bổ sung các trường chung cho tài liệu số (ddc/summary/language/isbn/materialType/tenantId)
    /// từ DB — index chunk cũ không chứa các trường này nên _reindex không lấy được.
    /// </summary>
    [HttpPost("enrich-digital-common")]
    [Permission("DIGITAL_DOC", "edit")]
    [SystemAdminOnly]
    public IActionResult EnrichDigitalCommon([FromServices] IBackgroundJobClient jobClient)
    {
        var jobId = jobClient.Enqueue<DigitalEnrichmentJob>(x => x.RunAsync(null, null));
        return Ok(ApiResponse<object>.Ok(new { jobId }, "Job làm giàu đã được thêm vào hàng đợi"));
    }

    /// <summary>
    /// Backfill/rebuild toàn bộ document metadata-only của tài liệu số vào index gộp (chạy nền qua
    /// Hangfire) — dùng khi cần dựng lại từ đầu thay vì chờ từng lần upload file tự enqueue.
    /// </summary>
    [HttpPost("rebuild-unified-index")]
    [Permission("DIGITAL_DOC", "edit")]
    [SystemAdminOnly]
    public IActionResult RebuildUnifiedIndex([FromServices] IBackgroundJobClient jobClient, [FromQuery] long? fromId = null, [FromQuery] long? toId = null)
    {
        var jobId = jobClient.Enqueue<DigitalUnifiedIndexingJob>(x => x.RunBulkAsync(fromId, toId, 500));
        return Ok(ApiResponse<object>.Ok(new { jobId }, "Job reindex metadata tài liệu số đã được thêm vào hàng đợi"));
    }

    /// <summary>Như trên nhưng chạy đồng bộ ngay trong request — chỉ dùng cho khoảng nhỏ (kiểm thử).</summary>
    [HttpPost("rebuild-unified-index-sync")]
    [Permission("DIGITAL_DOC", "edit")]
    [SystemAdminOnly]
    public async Task<IActionResult> RebuildUnifiedIndexSync([FromServices] DigitalUnifiedIndexingJob job, [FromQuery] long? fromId = null, [FromQuery] long? toId = null)
    {
        await job.RunBulkAsync(fromId, toId, 500);
        return Ok(ApiResponse<object>.Ok(new { }, "Đã reindex metadata tài liệu số xong"));
    }

    /// <summary>
    /// Đợt 26 — xóa toàn bộ index gộp rồi enqueue job build lại từ đầu cho cả tài liệu in lẫn tài liệu số.
    /// Chỉ dùng khi index bị hỏng/không đồng bộ — trong lúc job chạy, tìm kiếm OPAC sẽ tạm thời rỗng/thiếu.
    /// </summary>
    [HttpPost("rebuild-unified-index-from-scratch")]
    [Permission("DIGITAL_DOC", "edit")]
    [SystemAdminOnly]
    public async Task<IActionResult> RebuildUnifiedIndexFromScratch(
        [FromServices] IElasticsearchService elastic, [FromServices] IBackgroundJobClient jobClient)
    {
        await elastic.DeleteUnifiedIndexAsync();
        var printJobId   = jobClient.Enqueue<PrintBookIndexingJob>(j => j.RunBulkAsync(null, null, 500));
        var digitalJobId = jobClient.Enqueue<DigitalUnifiedIndexingJob>(j => j.RunBulkAsync(null, null, 500));
        return Ok(ApiResponse<object>.Ok(new { printJobId, digitalJobId },
            "Đã xóa index cũ và thêm job build lại từ đầu vào hàng đợi"));
    }

    // ── Server Z39.50 (Zebra / Index Data) ────────────────────────────────────
    // Đợt 26. Backend chỉ XUẤT file MARC ISO2709 ra thư mục dùng chung (ZebraExportJob); tiến trình Zebra
    // chạy ở container riêng, tự đánh chỉ mục thư mục đó thành 1 database gộp + 1 database mỗi đơn vị.
    // Xem backend/zebra/ và backend/docs/Z3950-SERVER.md.

    /// <summary>
    /// Xuất lại toàn bộ biểu ghi đã duyệt ra thư mục Zebra theo dõi (chạy nền). Tài khoản thuộc 1 đơn vị
    /// chỉ xuất lại được đơn vị mình; tài khoản hệ thống xuất 1 đơn vị (<paramref name="tenantId"/>) hoặc
    /// toàn bộ khi bỏ trống.
    /// </summary>
    [HttpPost("rebuild-zebra-export")]
    [Permission("CATALOG_BIBS", "edit")]
    public IActionResult RebuildZebraExport([FromServices] IBackgroundJobClient jobClient, [FromQuery] long? tenantId = null)
    {
        var effectiveTenantId = GetTenantId() ?? tenantId;
        var jobId = jobClient.Enqueue<ZebraExportJob>(j => j.RunBulkAsync(effectiveTenantId, null, null, 500));
        return Ok(ApiResponse<object>.Ok(new { jobId, tenantId = effectiveTenantId },
            "Đã thêm job xuất lại dữ liệu Z39.50 vào hàng đợi"));
    }

    /// <summary>Như trên nhưng chạy đồng bộ ngay trong request — dùng cho nút bấm ở trang quản trị.</summary>
    [HttpPost("rebuild-zebra-export-sync")]
    [Permission("CATALOG_BIBS", "edit")]
    public async Task<IActionResult> RebuildZebraExportSync([FromServices] ZebraExportJob job, [FromQuery] long? tenantId = null)
    {
        var effectiveTenantId = GetTenantId() ?? tenantId;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await job.RunBulkAsync(effectiveTenantId, null, null, 500);
        sw.Stop();
        return Ok(ApiResponse<object>.Ok(
            new { seconds = Math.Round(sw.Elapsed.TotalSeconds, 1), tenantId = effectiveTenantId },
            "Đã xuất lại xong"));
    }

    /// <summary>
    /// Trạng thái server Z39.50: thông tin kết nối + số biểu ghi đã xuất của từng database. Tài khoản
    /// thuộc 1 đơn vị chỉ thấy database đơn vị mình; tài khoản hệ thống thấy cả database gộp và mọi đơn vị.
    /// </summary>
    [HttpGet("zebra-export-status")]
    [Permission("CATALOG_BIBS", "view")]
    public async Task<IActionResult> ZebraExportStatus(
        [FromServices] IConfiguration config, [FromServices] ELIBAPIDbContext db)
    {
        var dir        = config["ZebraExport:Directory"]      ?? "/export";
        var combinedDb = config["ZebraExport:Database"]       ?? "ELIB";
        var dbPrefix   = config["ZebraExport:DatabasePrefix"] ?? "ELIB_";
        var callerTenantId = GetTenantId();

        // Map mã đơn vị -> tên đơn vị để hiển thị; tài khoản đơn vị chỉ lấy đơn vị mình.
        var tenantQuery = db.Tenants.AsNoTracking().Where(t => t.IsDelete != 2 && t.Code != null);
        if (callerTenantId != null) tenantQuery = tenantQuery.Where(t => t.Id == callerTenantId);
        var tenants = await tenantQuery.Select(t => new { t.Id, t.Code, t.Name }).ToListAsync();
        var nameByCode = tenants.Where(t => t.Code != null)
                                .ToDictionary(t => t.Code!, t => t.Name ?? t.Code!, StringComparer.OrdinalIgnoreCase);

        var databases    = new List<object>();
        long totalCount  = 0;
        DateTime? lastAt = null;

        if (Directory.Exists(dir))
        {
            foreach (var sub in Directory.GetDirectories(dir).OrderBy(d => d, StringComparer.Ordinal))
            {
                var code = Path.GetFileName(sub);
                // "_all" là thư mục hardlink nội bộ do container Zebra tự dựng cho database gộp — không
                // phải dữ liệu riêng, bỏ qua để không đếm trùng.
                if (string.Equals(code, "_all", StringComparison.Ordinal)) continue;
                // Tài khoản 1 đơn vị chỉ được thấy thư mục đơn vị mình.
                if (callerTenantId != null && !nameByCode.ContainsKey(code)) continue;

                // Quét thư mục MỘT lần (có đơn vị tới vài nghìn file, trang quản trị gọi endpoint này mỗi
                // lần mở) — vừa đếm vừa lấy thời điểm ghi gần nhất trong cùng vòng lặp.
                var count = 0;
                var maxAt = default(DateTime);
                foreach (var f in Directory.EnumerateFiles(sub, "*.iso"))
                {
                    count++;
                    var writtenAt = System.IO.File.GetLastWriteTimeUtc(f);
                    if (writtenAt > maxAt) maxAt = writtenAt;
                }
                if (maxAt != default && (lastAt == null || maxAt > lastAt)) lastAt = maxAt;
                totalCount += count;

                var isSystem = code.StartsWith('_');
                databases.Add(new
                {
                    code,
                    // Biểu ghi không thuộc đơn vị nào (tài khoản hệ thống tạo) chỉ nằm trong database gộp.
                    database    = isSystem ? null : dbPrefix + code,
                    tenantName  = isSystem ? "(Hệ thống)" : nameByCode.GetValueOrDefault(code, code),
                    recordCount = count,
                    lastExportAt = maxAt == default ? (DateTime?)null : maxAt,
                });
            }
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            host             = config["ZebraExport:Host"] ?? "localhost",
            port             = config["ZebraExport:Port"] ?? "2100",
            combinedDatabase = callerTenantId == null ? combinedDb : null,
            directory        = dir,
            directoryExists  = Directory.Exists(dir),
            totalCount,
            lastExportAt     = lastAt,
            databases,
        }));
    }
}
