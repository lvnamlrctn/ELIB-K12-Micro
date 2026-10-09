using Elib.BuildingBlocks.Domain;
using System.Linq.Expressions;
using System.Reflection;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Elib.BuildingBlocks.Persistence;

/// <summary>
/// DbContext gốc của mọi service. Tự gắn hai query filter có tên cho các entity tương ứng:
/// <list type="bullet">
/// <item><c>"Tenant"</c>: chỉ thấy dữ liệu đơn vị hiện tại, hoặc phạm vi <see cref="ITenantContext.ReadAcross"/>.
/// Ngữ cảnh chưa xác định/hệ thống thấy RỖNG — muốn đọc mọi đơn vị phải gọi <c>IgnoreQueryFilters([ElibQueryFilters.Tenant])</c> tường minh.</item>
/// <item><c>"SoftDelete"</c>: ẩn bản ghi IsDeleted.</item>
/// </list>
/// Service kế thừa phải gọi <c>base.OnModelCreating(modelBuilder)</c> SAU khi cấu hình entity của mình.
/// </summary>
public abstract class ElibDbContext(DbContextOptions options, ITenantContext tenant) : DbContext(options)
{
    private static readonly MethodInfo ConfigureFiltersMethod =
        typeof(ElibDbContext).GetMethod(nameof(ConfigureFilters), BindingFlags.Instance | BindingFlags.NonPublic)!;

    public ITenantContext Tenant { get; } = tenant;

    // Các thuộc tính dưới đây được EF đọc lại ở MỖI truy vấn (filter tham chiếu tới instance DbContext).
    private long? CurrentTenantId => Tenant.TenantId;
    private bool ReadAcrossEnabled => Tenant.ReadScope is not null;
    private long[] ReadScopeIds => Tenant.ReadScope?.ToArray() ?? [];

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Test chạy SQLite: SQLite không so sánh/sắp xếp được DateTimeOffset → lưu dạng số (giữ thứ tự với giá trị UTC).
        // PostgreSQL (production) dùng timestamptz như bình thường.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
                         .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?)))
                property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.DateTimeOffsetToBinaryConverter());
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Filter chỉ đặt trên gốc kế thừa; bỏ qua owned và shared-type (bảng nối nhiều-nhiều kiểu Dictionary — không có TenantId).
            if (entityType.BaseType is not null || entityType.IsOwned() || entityType.HasSharedClrType) continue;
            ConfigureFiltersMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }
    }

    private void ConfigureFilters<TEntity>(ModelBuilder modelBuilder) where TEntity : class
    {
        var builder = modelBuilder.Entity<TEntity>();

        if (typeof(ITenantOwned).IsAssignableFrom(typeof(TEntity)))
        {
            Expression<Func<TEntity, bool>> tenantFilter = e =>
                ReadAcrossEnabled
                    ? ReadScopeIds.Contains(EF.Property<long>(e, nameof(ITenantOwned.TenantId)))
                    : EF.Property<long>(e, nameof(ITenantOwned.TenantId)) == CurrentTenantId;
            builder.HasQueryFilter(ElibQueryFilters.Tenant, tenantFilter);
            builder.HasIndex(nameof(ITenantOwned.TenantId));
        }

        if (typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity)))
        {
            builder.HasQueryFilter(ElibQueryFilters.SoftDelete, e => !EF.Property<bool>(e, nameof(ISoftDeletable.IsDeleted)));
        }

        if (typeof(IHasPublicId).IsAssignableFrom(typeof(TEntity)))
        {
            builder.HasIndex(nameof(IHasPublicId.PublicId)).IsUnique();
        }
    }
}

public static class ElibQueryFilters
{
    public const string Tenant = "Tenant";
    public const string SoftDelete = "SoftDelete";
}
