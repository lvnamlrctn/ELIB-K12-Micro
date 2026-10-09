using Elib.Tenant.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Tenant.Application;

/// <summary>Cổng dữ liệu của service — Infrastructure (TenantDbContext) cài đặt.</summary>
public interface ITenantDb
{
    DbSet<Domain.Tenant> Tenants { get; }
    DbSet<TenantModuleLicense> Licenses { get; }
    DbSet<ModuleDefinition> Modules { get; }
    DbSet<SystemParameter> SystemParameters { get; }
    DbSet<Org> Orgs { get; }
    DbSet<Currency> Currencies { get; }
    DbSet<Nationality> Nationalities { get; }
    DbSet<Ethnicity> Ethnicities { get; }
    DbSet<AcademicTitle> AcademicTitles { get; }
    DbSet<Degree> Degrees { get; }
    DbSet<Position> Positions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Chạy <paramref name="work"/> trong một transaction, qua execution strategy (an toàn với retry của Npgsql).</summary>
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}

/// <summary>Nhận diện đơn vị (logo): logo là file của service media trên bucket công khai, phục vụ qua /s3/** ở gateway.</summary>
public sealed class BrandingOptions
{
    public const string SectionName = "Branding";

    /// <summary>Tiền tố đường dẫn bucket công khai của media (Storage:PublicPathPrefix + "/" + Media:PublicBucket).</summary>
    public string MediaPublicPrefix { get; set; } = "/s3/media-public";
}

public sealed class ProvisioningOptions
{
    public const string SectionName = "Provisioning";

    /// <summary>
    /// Service đang được triển khai và biết seed cho đơn vị mới. Chỉ các service này được chờ phản hồi —
    /// service chưa xây (theo lộ trình) không làm kẹt việc khởi tạo. Rỗng = kích hoạt ngay.
    /// </summary>
    public List<string> DeployedServices { get; set; } = [];

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromMinutes(1);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
