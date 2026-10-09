using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Dọn Payload/Result của AdminTask đã Completed/Cancelled quá hạn lưu (Đợt 13 — port nguyên văn
/// từ ELIB-LRC, không có phần cần sửa cho đa-tenant — dọn dữ liệu đã mã hoá/hết hạn lưu, không phải xem
/// nội dung nghiệp vụ). Chỉ tác động 2 trạng thái terminal này — Queued/Running/Paused/Failed/NeedsReview
/// không bao giờ là ứng viên (còn khả năng chạy/tiếp tục).</summary>
public class AdminTaskRetentionService(ELIBAPIDbContext db, IConfiguration config)
{
    private static readonly string[] ActiveStates = ["Queued", "Running", "Paused", "Failed", "NeedsReview"];

    private bool Enabled => config.GetValue("AdminTasks:Retention:Enabled", false);
    private bool DryRun => config.GetValue("AdminTasks:Retention:DryRun", true);
    private int PayloadDays => Math.Max(1, config.GetValue("AdminTasks:Retention:PayloadDays", 30));
    private int BatchSize => Math.Clamp(config.GetValue("AdminTasks:Retention:BatchSize", 100), 1, 1000);

    private IQueryable<AdminTask> Candidates(DateTime cutoff) => db.AdminTasks.Where(t =>
        t.PayloadPurgedAt == null && !t.RetentionHold && t.FinishedAt != null && t.FinishedAt < cutoff &&
        (t.State == "Completed" || t.State == "Cancelled") &&
        !db.AdminTasks.Any(e => e.SourcePreviewId == t.Id && ActiveStates.Contains(e.State)));

    public async Task<object> PreviewAsync(CancellationToken ct)
    {
        var payloadDays = PayloadDays; var batchSize = BatchSize;
        var cutoff = DateTime.UtcNow.AddDays(-payloadDays);
        var ids = await Candidates(cutoff).OrderBy(t => t.FinishedAt).Select(t => t.Id).ToListAsync(ct);
        var estimatedBytes = await EstimateBytesAsync(ids, ct);
        return new
        {
            enabled = Enabled, dryRun = DryRun, payloadDays, batchSize,
            candidateCount = ids.Count, estimatedBytes, checkedAt = DateTime.UtcNow
        };
    }

    public async Task<object> RunSweepAsync(bool? dryRunOverride, CancellationToken ct)
    {
        var startedAt = DateTime.UtcNow;
        if (!Enabled) return new { enabled = false, dryRun = true, taskCount = 0, chunkCount = 0, estimatedBytes = 0L };

        var dryRun = dryRunOverride ?? DryRun;
        var cutoff = DateTime.UtcNow.AddDays(-PayloadDays);
        var ids = await Candidates(cutoff).OrderBy(t => t.FinishedAt).Take(BatchSize).Select(t => t.Id).ToListAsync(ct);

        int taskCount = 0, chunkCount = 0;
        long estimatedBytes = await EstimateBytesAsync(ids, ct);

        if (!dryRun)
        {
            foreach (var id in ids)
            {
                chunkCount += await db.AdminTaskChunks.Where(c => c.TaskId == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.Payload, (string?)null).SetProperty(c => c.Result, (string?)null), ct);
                var claimed = await db.AdminTasks.Where(t => t.Id == id && t.PayloadPurgedAt == null && !t.RetentionHold)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.Payload, (string?)null).SetProperty(t => t.Result, (string?)null)
                        .SetProperty(t => t.PayloadPurgedAt, DateTime.UtcNow).SetProperty(t => t.ResultPurgedAt, DateTime.UtcNow), ct);
                if (claimed == 1) taskCount++;
            }
        }
        else taskCount = ids.Count;

        var finishedAt = DateTime.UtcNow;
        db.AdminTaskRetentionRuns.Add(new AdminTaskRetentionRun
        {
            StartedAt = startedAt, FinishedAt = finishedAt, DryRun = dryRun,
            TaskCount = taskCount, ChunkCount = chunkCount, EstimatedBytes = estimatedBytes
        });
        await db.SaveChangesAsync(ct);

        return new { enabled = true, dryRun, taskCount, chunkCount, estimatedBytes, startedAt, finishedAt };
    }

    public async Task<List<AdminTaskRetentionRun>> RecentRunsAsync(CancellationToken ct) =>
        await db.AdminTaskRetentionRuns.AsNoTracking().OrderByDescending(r => r.StartedAt).Take(50).ToListAsync(ct);

    public Task<int> SetHoldAsync(Guid taskId, bool hold, CancellationToken ct) =>
        db.AdminTasks.Where(t => t.Id == taskId).ExecuteUpdateAsync(s => s.SetProperty(t => t.RetentionHold, hold), ct);

    private async Task<long> EstimateBytesAsync(List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return 0;
        var taskLen = await db.AdminTasks.Where(t => ids.Contains(t.Id))
            .SumAsync(t => (long?)((t.Payload ?? "").Length + (t.Result ?? "").Length), ct) ?? 0;
        var chunkLen = await db.AdminTaskChunks.Where(c => ids.Contains(c.TaskId))
            .SumAsync(c => (long?)((c.Payload ?? "").Length + (c.Result ?? "").Length), ct) ?? 0;
        return taskLen + chunkLen;
    }
}
