using System.Text.Json;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Patron.Application;
using Elib.Patron.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elib.Patron.Infrastructure;

/// <summary>
/// Chạy nền các lượt nhập bạn đọc đã bấm chạy (ELIB-LRC: nhập background tới 10.000 dòng — ở đây tới 50.000). Lập lại kế hoạch trên dữ liệu
/// mới nhất, ghi từng dòng (mỗi dòng một SaveChanges: hồ sơ + event outbox), theo lô 200 dòng với scope/DbContext mới để bộ theo dõi
/// thay đổi không phình ra. Dòng lỗi bỏ qua và ghi vào file "Dòng cần sửa". Khởi động lại: lượt đang chờ chạy tiếp, lượt đang chạy dở
/// đánh dấu lỗi (đã ghi dòng nào giữ dòng đó). Lượt nhập cũ hơn 7 ngày xoá hẳn (cả file gốc).
/// </summary>
public sealed partial class ReaderImportWorker(IServiceScopeFactory scopes, IReaderImportDispatcher queue, TimeProvider clock,
    ILogger<ReaderImportWorker> logger) : BackgroundService
{
    public const int BatchSize = 200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), clock, stoppingToken); // để migrate/bản sao đơn vị chạy xong
            await RecoverAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            LogRecoverFailed(logger, ex);
        }

        await foreach (var (tenantId, jobId) in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await RunAsync(tenantId, jobId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogJobFailed(logger, ex, jobId, tenantId);
                await MarkFailedAsync(tenantId, jobId, "Lỗi hệ thống khi nhập — các dòng đã ghi được giữ lại; xem trước rồi chạy lại phần còn lại.", stoppingToken);
            }
        }
    }

    public async Task RunAsync(long tenantId, long jobId, CancellationToken ct)
    {
        ReaderImportOptions options;
        IReadOnlyList<string> headers;
        List<ReaderImportPlan> plans;
        using (var scope = scopes.CreateScope())
        {
            var sp = scope.ServiceProvider;
            using var _ = sp.GetRequiredService<ITenantContext>().Use(tenantId);
            var db = sp.GetRequiredService<PatronDbContext>();
            var job = await db.Set<ReaderImportJob>().FirstOrDefaultAsync(j => j.Id == jobId, ct);
            if (job is not { Status: ReaderImportStatus.Queued }) return;
            job.Start(clock.GetUtcNow());
            await db.SaveChangesAsync(ct);

            options = JsonSerializer.Deserialize<ReaderImportOptions>(job.OptionsJson, ReaderImportService.Json)
                ?? throw new InvalidOperationException("Tuỳ chọn nhập hỏng.");
            (headers, var rows) = ReaderWorkbook.Read(new MemoryStream(job.File), options);
            plans = [.. await sp.GetRequiredService<ReaderResource>().PlanImportAsync(rows, options, ct)];
        }

        var failed = plans.Where(p => p.Error is not null).ToList();
        var todo = plans.Where(p => p.Error is null).ToList();
        int created = 0, updated = 0, processed = failed.Count;
        foreach (var batch in todo.Chunk(BatchSize))
        {
            using var scope = scopes.CreateScope();
            var sp = scope.ServiceProvider;
            using var _ = sp.GetRequiredService<ITenantContext>().Use(tenantId);
            var readers = sp.GetRequiredService<ReaderResource>();
            foreach (var plan in batch)
            {
                try
                {
                    if (await readers.ApplyImportRowAsync(plan, options, ct)) created++;
                    else updated++;
                }
                catch (BusinessRuleException ex)
                {
                    failed.Add(plan with { Error = ex.Message });
                }
                processed++;
            }
            var db = sp.GetRequiredService<PatronDbContext>();
            var job = await db.Set<ReaderImportJob>().FirstAsync(j => j.Id == jobId, ct);
            job.Progress(processed, created, updated, failed.Count);
            await db.SaveChangesAsync(ct);
        }

        using (var scope = scopes.CreateScope())
        {
            var sp = scope.ServiceProvider;
            using var _ = sp.GetRequiredService<ITenantContext>().Use(tenantId);
            var db = sp.GetRequiredService<PatronDbContext>();
            var job = await db.Set<ReaderImportJob>().FirstAsync(j => j.Id == jobId, ct);
            failed.Sort((a, b) => a.Row.Number.CompareTo(b.Row.Number));
            job.Progress(processed, created, updated, failed.Count);
            job.Finish(clock.GetUtcNow(), failed.Count > 0 ? ReaderWorkbook.ErrorFile(headers, failed) : null, ReaderImportService.ErrorsJson(failed));
            await db.SaveChangesAsync(ct);
            await sp.GetRequiredService<ReaderResource>().RecordImportAuditAsync(
                $"nhập Excel {job.FileName}: thêm {created}, cập nhật {updated}, lỗi {failed.Count}", ct);
        }
    }

    private async Task RecoverAsync(CancellationToken ct)
    {
        List<long> tenants;
        using (var scope = scopes.CreateScope())
        {
            using var system = scope.ServiceProvider.GetRequiredService<ITenantContext>().UseSystem();
            tenants = await scope.ServiceProvider.GetRequiredService<PatronDbContext>().Set<TenantReplicaRecord>().AsNoTracking()
                .Select(t => t.TenantId).ToListAsync(ct);
        }
        var cutoff = clock.GetUtcNow() - ReaderImportJob.Retention;
        foreach (var tenantId in tenants)
        {
            using var scope = scopes.CreateScope();
            using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
            var db = scope.ServiceProvider.GetRequiredService<PatronDbContext>();
            await db.Set<ReaderImportJob>().IgnoreQueryFilters().Where(j => j.TenantId == tenantId && j.CreatedAt < cutoff).ExecuteDeleteAsync(ct);
            foreach (var job in await db.Set<ReaderImportJob>().Where(j => j.Status == ReaderImportStatus.Queued || j.Status == ReaderImportStatus.Running).ToListAsync(ct))
            {
                if (job.Status == ReaderImportStatus.Queued) queue.Enqueue(tenantId, job.Id);
                else job.Fail(clock.GetUtcNow(), "Service khởi động lại khi đang nhập — các dòng đã ghi được giữ lại; xem trước rồi chạy lại phần còn lại.");
            }
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task MarkFailedAsync(long tenantId, long jobId, string message, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
            var db = scope.ServiceProvider.GetRequiredService<PatronDbContext>();
            if (await db.Set<ReaderImportJob>().FirstOrDefaultAsync(j => j.Id == jobId, ct) is { } job)
            {
                job.Fail(clock.GetUtcNow(), message);
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogJobFailed(logger, ex, jobId, tenantId);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Lượt nhập bạn đọc {JobId} của đơn vị {TenantId} lỗi")]
    private static partial void LogJobFailed(ILogger logger, Exception exception, long jobId, long tenantId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Không khôi phục được các lượt nhập bạn đọc khi khởi động")]
    private static partial void LogRecoverFailed(ILogger logger, Exception exception);
}
