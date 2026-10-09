using System.Text.Json;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Jobs;

/// <summary>
/// Đợt 22.4 — port từ ELIB-LRC, không đổi gì (bucket riêng tư dùng chung mọi đơn vị, không tách theo
/// tenant được — xem ghi chú ở DigitalStorageAuditResult). Đối soát toàn bộ object trên bucket riêng tư
/// (MinIO) với bảng EbookFile — tìm file "mồ côi" (có object thật nhưng KHÔNG có dòng EbookFile nào tham
/// chiếu tới) + tổng dung lượng đang dùng. Ghi kết quả vào bảng nhỏ DigitalStorageAuditResult, dashboard
/// (tab DevOps) chỉ đọc bản ghi mới nhất.
///
/// So khớp bằng cách kiểm tra Url của từng EbookFile có CHỨA object name hay không (chấp nhận cả trường
/// hợp Url lưu tuyệt đối lẫn tương đối) — không đòi hỏi khớp tuyệt đối 1-1. Chiều ngược lại (BrokenFile)
/// dùng cùng phép so khớp: EbookFile đang hoạt động nhưng Url không CHỨA object name nào còn tồn tại thật
/// trên bucket — nghĩa là file đã bị xóa/hỏng link.
/// </summary>
public class DigitalStorageAuditJob(
    ELIBAPIDbContext db,
    IMinioService minio,
    ILogger<DigitalStorageAuditJob> logger)
{
    private const int SampleSize = 100;

    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync()
    {
        var files = await db.EbookFiles
            .Where(f => f.IsDelete != 2 && f.Url != null && f.Url != "")
            .Select(f => new { f.PublicId, f.EbookId, Url = f.Url! })
            .ToListAsync();

        long totalObjects = 0, totalSize = 0, orphanCount = 0;
        var orphans = new List<(string Name, long Size)>();
        var liveObjectNames = new List<string>();

        await foreach (var (objectName, size) in minio.ListPrivateBucketObjectsAsync())
        {
            totalObjects++;
            totalSize += size;
            liveObjectNames.Add(objectName);
            var referenced = files.Any(f => f.Url.Contains(objectName, StringComparison.OrdinalIgnoreCase));
            if (!referenced)
            {
                orphanCount++;
                if (orphans.Count < SampleSize) orphans.Add((objectName, size));
            }
        }

        var broken = files
            .Where(f => !liveObjectNames.Any(n => f.Url.Contains(n, StringComparison.OrdinalIgnoreCase)))
            .Take(SampleSize)
            .Select(f => new { publicId = f.PublicId, ebookId = f.EbookId, url = f.Url })
            .ToList();
        long brokenCount = files.Count(f => !liveObjectNames.Any(n => f.Url.Contains(n, StringComparison.OrdinalIgnoreCase)));

        db.DigitalStorageAuditResults.Add(new DigitalStorageAuditResult
        {
            RunAt = DateTime.UtcNow,
            TotalObjects = totalObjects,
            TotalSizeBytes = totalSize,
            OrphanCount = orphanCount,
            DbFileCount = files.Count,
            OrphanSampleJson = JsonSerializer.Serialize(orphans.Select(o => new { name = o.Name, size = o.Size })),
            BrokenFileCount = brokenCount,
            BrokenSampleJson = JsonSerializer.Serialize(broken)
        });
        await db.SaveChangesAsync();

        logger.LogInformation(
            "DigitalStorageAuditJob: {TotalObjects} object, {TotalSizeMb}MB, {OrphanCount} mồ côi, {BrokenCount} hỏng",
            totalObjects, totalSize / 1024 / 1024, orphanCount, brokenCount);
    }
}
