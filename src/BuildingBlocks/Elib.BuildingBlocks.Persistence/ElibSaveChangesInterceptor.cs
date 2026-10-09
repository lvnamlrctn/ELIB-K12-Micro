using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Elib.BuildingBlocks.Persistence;

/// <summary>
/// Chạy trước mỗi SaveChanges:
/// <list type="bullet">
/// <item>Thêm mới <see cref="ITenantOwned"/>: gán TenantId từ ngữ cảnh; ghi sang đơn vị khác hoặc thiếu ngữ cảnh → từ chối.</item>
/// <item>Sửa TenantId hoặc ghi trong lúc đọc chéo → từ chối.</item>
/// <item>Gán PublicId (Guid v7), cột audit; xoá <see cref="ISoftDeletable"/> chuyển thành xoá mềm.</item>
/// </list>
/// Ngữ cảnh hệ thống được thêm dữ liệu cho đơn vị bất kỳ nếu đặt TenantId tường minh (seed khi khởi tạo đơn vị).
/// </summary>
public sealed class ElibSaveChangesInterceptor(ITenantContext tenant, ICurrentActor actor, TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;
        var now = clock.GetUtcNow();
        var actorId = actor.Id;

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State is EntityState.Unchanged or EntityState.Detached) continue;

            if (entry.Entity is ITenantOwned owned) GuardTenant(entry, owned);

            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity is IHasPublicId p && p.PublicId == Guid.Empty) p.PublicId = Guid.CreateVersion7();
                    if (entry.Entity is IAuditable a)
                    {
                        a.CreatedAt = now;
                        a.CreatedBy ??= actorId;
                    }
                    break;

                case EntityState.Modified:
                    if (entry.Entity is IAuditable m)
                    {
                        m.UpdatedAt = now;
                        m.UpdatedBy = actorId;
                        entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                        entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
                    }
                    break;

                case EntityState.Deleted when entry.Entity is ISoftDeletable d:
                    entry.State = EntityState.Modified;
                    d.IsDeleted = true;
                    d.DeletedAt = now;
                    d.DeletedBy = actorId;
                    if (entry.Entity is IAuditable da)
                    {
                        da.UpdatedAt = now;
                        da.UpdatedBy = actorId;
                    }
                    break;
            }
        }
    }

    private void GuardTenant(EntityEntry entry, ITenantOwned owned)
    {
        if (tenant.ReadScope is not null)
            throw new TenantAccessDeniedException("Không được ghi dữ liệu khi đang bật đọc chéo đơn vị.");

        if (entry.State == EntityState.Added)
        {
            if (tenant.IsSystem)
            {
                if (owned.TenantId <= 0)
                    throw new TenantRequiredException($"Ngữ cảnh hệ thống phải đặt TenantId tường minh khi thêm {entry.Metadata.DisplayName()}.");
                return;
            }

            var current = tenant.RequireTenantId();
            if (owned.TenantId == 0) owned.TenantId = current;
            else if (owned.TenantId != current)
                throw new TenantAccessDeniedException($"Không được thêm {entry.Metadata.DisplayName()} cho đơn vị khác.");
            return;
        }

        var property = entry.Property(nameof(ITenantOwned.TenantId));
        if (property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
            throw new TenantAccessDeniedException($"Không được đổi TenantId của {entry.Metadata.DisplayName()}.");

        if (!tenant.IsSystem && owned.TenantId != tenant.RequireTenantId())
            throw new TenantAccessDeniedException($"Không được sửa/xoá {entry.Metadata.DisplayName()} của đơn vị khác.");
    }
}
