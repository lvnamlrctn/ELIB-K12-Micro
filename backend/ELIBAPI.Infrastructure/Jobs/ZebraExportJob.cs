using ELIBAPI.Core.Common;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Jobs;

/// <summary>
/// Đợt 22.3 — port từ ELIB-LRC. Xuất biểu ghi <c>PrintBook.Bib</c> ra file MARC ISO2709 (<c>{bibid}.iso</c>)
/// trong thư mục dùng chung với 1 tiến trình Zebra (Index Data) — Zebra tự định kỳ <c>zebraidx update</c> thư
/// mục này để phục vụ Z39.50/SRU. Dựng bản ghi qua <see cref="MarcRecordBuilder"/> (dùng chung với OAI-PMH).
/// Khác LRC (đơn tenant, 1 thư mục xuất): ELIB xuất MỖI ĐƠN VỊ 1 THƯ MỤC CON riêng (theo <c>Tenant.Code</c>,
/// biểu ghi không có đơn vị — tài khoản hệ thống tạo — rơi vào thư mục "_system") để sau này mỗi đơn vị có
/// thể chạy 1 tiến trình Zebra độc lập (database Z39.50 riêng theo đơn vị), đúng ghi chú vận hành
/// <c>Z3950-SRU-SERVER.md</c> gốc "database theo tenant". KHÔNG dựng tiến trình Zebra/Index Data thật ở đây —
/// đó là việc hạ tầng/ops ngoài phạm vi code, xem tài liệu vận hành khi triển khai thật.
/// </summary>
public class ZebraExportJob(
    ELIBAPIDbContext         db,
    IConfiguration           config,
    ILogger<ZebraExportJob>  logger)
{
    private const string ApprovedStatus = "f";
    private const string NoTenantFolder = "_system";

    private string ExportDir => config["ZebraExport:Directory"] ?? "/export";

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 900])]
    public async Task RunAsync(long bibId)
    {
        var tenantCode = await TenantCodeForBibAsync(bibId);
        var record     = await MarcRecordBuilder.BuildAsync(db, bibId);

        // Biểu ghi đã bị xóa mềm hoặc rút duyệt → gỡ khỏi CẢ 2 thư mục có thể (đơn vị hiện tại + hệ thống,
        // phòng khi biểu ghi vừa đổi đơn vị giữa 2 lần chạy) để không để rác file cũ, Zebra sẽ gỡ khỏi
        // chỉ mục ở lần zebraidx update kế tiếp.
        if (record == null)
        {
            RemoveFile(RecordPath(tenantCode, bibId));
            if (tenantCode != NoTenantFolder) RemoveFile(RecordPath(NoTenantFolder, bibId));
            return;
        }

        Directory.CreateDirectory(Path.Combine(ExportDir, tenantCode));
        await File.WriteAllBytesAsync(RecordPath(tenantCode, bibId), Iso2709Reader.Write([record]));
    }

    /// <summary>Gỡ 1 biểu ghi khỏi thư mục xuất — gọi khi xóa (mềm hoặc cứng).</summary>
    public async Task RemoveAsync(long bibId)
    {
        var tenantCode = await TenantCodeForBibAsync(bibId);
        RemoveFile(RecordPath(tenantCode, bibId));
    }

    /// <summary>Xuất lại toàn bộ (hoặc 1 khoảng Bibid) biểu ghi đã duyệt — CỦA MỘT ĐƠN VỊ, hoặc toàn hệ
    /// thống nếu <paramref name="tenantId"/> = null (chỉ tài khoản hệ thống được gọi — kiểm ở controller).</summary>
    [AutomaticRetry(Attempts = 0)]
    public async Task RunBulkAsync(long? tenantId, long? fromId = null, long? toId = null, int batchSize = 500)
    {
        var idQuery = db.Bibs.AsNoTracking().Where(b => b.IsDelete != 2 && b.Status == ApprovedStatus);
        if (tenantId.HasValue) idQuery = idQuery.Where(b => b.TenantId == tenantId);
        if (fromId.HasValue)   idQuery = idQuery.Where(b => b.Bibid >= fromId.Value);
        if (toId.HasValue)     idQuery = idQuery.Where(b => b.Bibid <= toId.Value);

        var ids = await idQuery.OrderBy(b => b.Bibid).Select(b => b.Bibid).ToListAsync();
        logger.LogInformation("ZebraExportJob: bắt đầu xuất {Count} biểu ghi (tenant {TenantId})", ids.Count, tenantId);

        var tenantCodes = await TenantCodeMapAsync();
        var done = 0;
        foreach (var chunk in ids.Chunk(batchSize))
        {
            foreach (var bibId in chunk)
            {
                var record = await MarcRecordBuilder.BuildAsync(db, bibId);
                if (record == null) continue;
                var tc = await TenantCodeForBibAsync(bibId, tenantCodes);
                Directory.CreateDirectory(Path.Combine(ExportDir, tc));
                await File.WriteAllBytesAsync(RecordPath(tc, bibId), Iso2709Reader.Write([record]));
            }
            done += chunk.Length;
            logger.LogInformation("ZebraExportJob: {Done}/{Total}", done, ids.Count);
        }
        logger.LogInformation("ZebraExportJob: xong {Count} biểu ghi", done);
    }

    private static void RemoveFile(string path) { if (File.Exists(path)) File.Delete(path); }

    private string RecordPath(string tenantCode, long bibId) => Path.Combine(ExportDir, tenantCode, $"{bibId}.iso");

    /// <summary>Đợt 26 — mã đơn vị đi thẳng vào đường dẫn file VÀ tên database Z39.50, nên chỉ giữ lại ký
    /// tự an toàn (chặn luôn path traversal: mọi dấu chấm/gạch chéo đều bị loại). Dữ liệu thực tế theo quy
    /// ước mã 2-3 ký tự hoa ASCII nên hàm này chỉ là lưới an toàn cuối, không phải đường đi thường.</summary>
    private static string SafeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return NoTenantFolder;
        var cleaned = new string(code.Where(c => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-').ToArray());
        return cleaned.Length == 0 ? NoTenantFolder : cleaned;
    }

    private async Task<Dictionary<long, string>> TenantCodeMapAsync() =>
        await db.Tenants.AsNoTracking().Where(t => t.IsDelete != 2 && t.Code != null)
            .ToDictionaryAsync(t => t.Id, t => SafeCode(t.Code));

    private async Task<string> TenantCodeForBibAsync(long bibId, Dictionary<long, string>? cache = null)
    {
        var tenantId = await db.Bibs.AsNoTracking().Where(b => b.Bibid == bibId)
            .Select(b => (long?)b.TenantId).FirstOrDefaultAsync();
        if (!tenantId.HasValue) return NoTenantFolder;
        if (cache != null) return cache.GetValueOrDefault(tenantId.Value, NoTenantFolder);
        var code = await db.Tenants.AsNoTracking().Where(t => t.Id == tenantId.Value).Select(t => t.Code).FirstOrDefaultAsync();
        return SafeCode(code);
    }
}
