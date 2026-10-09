using Elib.Contracts.Events;
using Elib.Contracts.Events.Platform;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Elib.BuildingBlocks.TenantReplica;

/// <summary>
/// Seed dữ liệu mặc định của service cho đơn vị mới (vai trò mẫu, chính sách mặc định…).
/// PHẢI idempotent: khởi tạo có thể chạy lại. Chạy trong ngữ cảnh của đơn vị mới.
/// </summary>
public interface ITenantSeeder
{
    Task SeedAsync(TenantProvisioned tenant, CancellationToken cancellationToken);
}

/// <summary>Tên service dùng khi báo TenantSeeded.</summary>
public sealed record TenantReplicaServiceName(string Value);

/// <summary>Đơn vị mới: ghi bản sao, chạy seeder, báo kết quả cho saga khởi tạo ở service tenant (docs 03 §5.3).</summary>
public sealed partial class TenantProvisionedConsumer<TDbContext>(
    TDbContext db, IEnumerable<ITenantSeeder> seeders, TenantReplicaServiceName service, TimeProvider clock,
    HybridCache cache, ILogger<TenantProvisionedConsumer<TDbContext>> logger)
    : IConsumer<TenantProvisioned>
    where TDbContext : DbContext
{
    public async Task Consume(ConsumeContext<TenantProvisioned> context)
    {
        var message = context.Message;
        var replica = await db.Set<TenantReplicaRecord>().Include(t => t.Modules)
            .FirstOrDefaultAsync(t => t.TenantId == message.TenantId, context.CancellationToken);
        if (replica is null)
        {
            replica = new TenantReplicaRecord { TenantId = message.TenantId };
            db.Add(replica);
        }

        // TenantProvisioned không mang version: chỉ điền khi bản sao chưa nhận event có version nào.
        if (replica.SourceVersion == 0)
        {
            replica.Code = message.Code;
            replica.Name = message.Name;
            replica.Subdomain = message.Subdomain;
            replica.TimeZone = message.TimeZone;
            replica.Status = nameof(TenantStatus.Provisioning);
        }
        if (replica.LicenseVersion == 0) ReplicaWriter.ReplaceModules(replica, message.Modules);
        replica.SyncedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(context.CancellationToken);
        await ReplicaWriter.InvalidateAsync(cache, message.TenantId, context.CancellationToken);

        string? error = null;
        try
        {
            foreach (var seeder in seeders) await seeder.SeedAsync(message, context.CancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSeedFailed(logger, ex, message.TenantId, service.Value);
            error = ex.Message;
        }

        await context.Publish(new TenantSeeded
        {
            TenantId = message.TenantId,
            Actor = EventActor.System,
            Service = service.Value,
            Succeeded = error is null,
            Error = error,
        }, context.CancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Seed đơn vị {TenantId} ở service {Service} thất bại")]
    private static partial void LogSeedFailed(ILogger logger, Exception exception, long tenantId, string service);
}

public sealed class TenantUpdatedConsumer<TDbContext>(TDbContext db, TimeProvider clock, HybridCache cache) : IConsumer<TenantUpdated>
    where TDbContext : DbContext
{
    public async Task Consume(ConsumeContext<TenantUpdated> context)
    {
        if (!await ReplicaWriter.ApplyTenantAsync(db, context.Message, clock.GetUtcNow(), context.CancellationToken)) return;
        await db.SaveChangesAsync(context.CancellationToken);
        await ReplicaWriter.InvalidateAsync(cache, context.Message.TenantId, context.CancellationToken);
    }
}

public sealed class ModuleLicenseChangedConsumer<TDbContext>(TDbContext db, TimeProvider clock, HybridCache cache) : IConsumer<ModuleLicenseChanged>
    where TDbContext : DbContext
{
    public async Task Consume(ConsumeContext<ModuleLicenseChanged> context)
    {
        if (!await ReplicaWriter.ApplyLicensesAsync(db, context.Message, clock.GetUtcNow(), context.CancellationToken)) return;
        await db.SaveChangesAsync(context.CancellationToken);
        await ReplicaWriter.InvalidateAsync(cache, context.Message.TenantId, context.CancellationToken);
    }
}

internal static class ReplicaWriter
{
    public static string CacheTag(long tenantId) => $"tenant-replica:{tenantId}";

    /// <summary>Ghi thông tin đơn vị vào bản sao nếu mới hơn bản đang có. false = bỏ qua (event cũ / gửi lại). Chưa SaveChanges.</summary>
    public static async Task<bool> ApplyTenantAsync(DbContext db, TenantUpdated m, DateTimeOffset now, CancellationToken ct)
    {
        var replica = await db.Set<TenantReplicaRecord>().FirstOrDefaultAsync(t => t.TenantId == m.TenantId, ct);
        if (replica is null)
        {
            replica = new TenantReplicaRecord { TenantId = m.TenantId };
            db.Add(replica);
        }
        else if (replica.SourceVersion >= m.SourceVersion)
        {
            return false; // event cũ hơn bản đang có (đến trễ / gửi lại)
        }

        replica.Code = m.Code;
        replica.Name = m.Name;
        replica.Subdomain = m.Subdomain;
        replica.TimeZone = m.TimeZone;
        replica.Status = m.Status.ToString();
        replica.SourceVersion = m.SourceVersion;
        replica.SyncedAt = now;
        return true;
    }

    /// <summary>Ghi tập license vào bản sao nếu mới hơn bản đang có. false = bỏ qua. Chưa SaveChanges.</summary>
    public static async Task<bool> ApplyLicensesAsync(DbContext db, ModuleLicenseChanged m, DateTimeOffset now, CancellationToken ct)
    {
        var replica = await db.Set<TenantReplicaRecord>().Include(t => t.Modules).FirstOrDefaultAsync(t => t.TenantId == m.TenantId, ct);
        if (replica is null)
        {
            replica = new TenantReplicaRecord { TenantId = m.TenantId };
            db.Add(replica);
        }
        else if (replica.LicenseVersion >= m.SourceVersion)
        {
            return false;
        }

        ReplaceModules(replica, m.Modules);
        replica.LicenseVersion = m.SourceVersion;
        replica.SyncedAt = now;
        return true;
    }

    /// <summary>Thay tập module bằng cập nhật tại chỗ — xoá rồi thêm cùng khoá (TenantId, ModuleCode) sẽ trùng entity đang theo dõi.</summary>
    public static void ReplaceModules(TenantReplicaRecord replica, IEnumerable<ModuleLicense> modules)
    {
        var incoming = modules.GroupBy(x => x.ModuleCode, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        replica.Modules.RemoveAll(m => !incoming.ContainsKey(m.ModuleCode));
        foreach (var (code, license) in incoming)
        {
            var row = replica.Modules.FirstOrDefault(m => m.ModuleCode == code);
            if (row is null)
            {
                row = new TenantModuleReplica { TenantId = replica.TenantId, ModuleCode = code };
                replica.Modules.Add(row);
            }
            row.Status = license.Status;
            row.ValidFrom = license.ValidFrom;
            row.ValidTo = license.ValidTo;
        }
    }

    public static Task InvalidateAsync(HybridCache cache, long tenantId, CancellationToken ct) =>
        cache.RemoveByTagAsync(CacheTag(tenantId), ct).AsTask();
}
