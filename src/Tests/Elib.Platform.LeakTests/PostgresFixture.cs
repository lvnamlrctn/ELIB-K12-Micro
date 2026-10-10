using Elib.Audit.Infrastructure;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.Identity.Infrastructure;
using Elib.Media.Infrastructure;
using Elib.Notification.Infrastructure;
using Elib.Catalog.Infrastructure;
using Elib.Circulation.Infrastructure;
using Elib.Holdings.Infrastructure;
using Elib.Patron.Infrastructure;
using Elib.Tenant.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Elib.Platform.LeakTests;

/// <summary>Chỉ chạy khi có PostgreSQL thật (biến ELIB_TEST_POSTGRES = chuỗi kết nối superuser) — CI đặt biến này.</summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(PostgresFixture.AdminConnectionString))
            Skip = "Cần PostgreSQL: đặt ELIB_TEST_POSTGRES (vd Host=localhost;Username=postgres;Password=...).";
    }
}

/// <summary>Một service có DB riêng: tên DB thử nghiệm và cách tạo DbContext của service với ngữ cảnh đơn vị cho trước.</summary>
public sealed record ServiceDatabase(string Service, string Database, string[] ExpectedRlsTables, Func<string, ITenantContext, DbContext> Create)
{
    public override string ToString() => Service;
}

/// <summary>
/// Tạo DB của từng service trên PostgreSQL thật, sở hữu bởi một role ứng dụng KHÔNG phải superuser, không BYPASSRLS
/// (như role identity_app, tenant_app… trên môi trường thật), rồi chạy migration thật của service bằng role đó.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string AppRole = "elib_leak_app";
    private const string AppPassword = "elib-leak-app";

    public static string? AdminConnectionString => Environment.GetEnvironmentVariable("ELIB_TEST_POSTGRES");

    public static readonly ServiceDatabase[] Services =
    [
        new("identity", "elib_leak_identity", ["staff_users", "roles"],
            (cs, t) => new IdentityDbContext(Options<IdentityDbContext>(cs, t, o => o.UseOpenIddict()), t)),
        new("tenant", "elib_leak_tenant", ["system_parameters", "orgs", "tenant_module_licenses"],
            (cs, t) => new TenantDbContext(Options<TenantDbContext>(cs, t), t)),
        new("notification", "elib_leak_notification", ["email_settings", "email_templates", "notification_logs"],
            (cs, t) => new NotificationDbContext(Options<NotificationDbContext>(cs, t), t)),
        new("audit", "elib_leak_audit", ["audit_logs"],
            (cs, t) => new AuditDbContext(Options<AuditDbContext>(cs, t), t)),
        new("media", "elib_leak_media", ["media_files"],
            (cs, t) => new MediaDbContext(Options<MediaDbContext>(cs, t), t)),
        new("patron", "elib_leak_patron", ["readers", "reader_types", "classes", "courses", "reader_groups"],
            (cs, t) => new PatronDbContext(Options<PatronDbContext>(cs, t), t)),
        new("catalog", "elib_leak_catalog", ["bibs", "bib_types", "worksheets"],
            (cs, t) => new CatalogDbContext(Options<CatalogDbContext>(cs, t), t)),
        new("holdings", "elib_leak_holdings", ["store_types", "stores", "items", "bib_snapshots"],
            (cs, t) => new HoldingsDbContext(Options<HoldingsDbContext>(cs, t), t)),
        new("circulation", "elib_leak_circulation", ["circ_places", "loan_policies", "loans", "patron_replicas", "item_replicas", "bib_snapshots"],
            (cs, t) => new CirculationDbContext(Options<CirculationDbContext>(cs, t), t)),
    ];

    public static ServiceDatabase Get(string service) => Services.Single(s => s.Service == service);

    /// <summary>Chuỗi kết nối bằng role ứng dụng tới DB của service.</summary>
    public static string AppConnection(ServiceDatabase db) => new NpgsqlConnectionStringBuilder(AdminConnectionString)
    {
        Database = db.Database, Username = AppRole, Password = AppPassword, Pooling = true,
    }.ConnectionString;

    public static string AdminConnection(string database) => new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = database }.ConnectionString;

    public static DbContext Open(ServiceDatabase db, ITenantContext tenant) => db.Create(AppConnection(db), tenant);

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(AdminConnectionString)) return;

        await using (var admin = new NpgsqlConnection(AdminConnection("postgres")))
        {
            await admin.OpenAsync();
            await Exec(admin, $"""
                DO $$ BEGIN
                  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{AppRole}') THEN
                    CREATE ROLE {AppRole} LOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE PASSWORD '{AppPassword}';
                  END IF;
                END $$;
                """);
            foreach (var db in Services)
            {
                await Exec(admin, $"DROP DATABASE IF EXISTS {db.Database} WITH (FORCE)");
                await Exec(admin, $"CREATE DATABASE {db.Database} OWNER {AppRole}");
            }
        }

        foreach (var db in Services)
        {
            // Như MigrateElibDatabaseAsync: migration chạy trong ngữ cảnh hệ thống, bằng chính role ứng dụng.
            var tenant = new TenantContext();
            using var system = tenant.UseSystem();
            await using var context = Open(db, tenant);
            await context.Database.MigrateAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(AdminConnectionString)) return;
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(AdminConnection("postgres"));
        await admin.OpenAsync();
        foreach (var db in Services) await Exec(admin, $"DROP DATABASE IF EXISTS {db.Database} WITH (FORCE)");
    }

    private static DbContextOptions<T> Options<T>(string connectionString, ITenantContext tenant, Action<DbContextOptionsBuilder>? configure = null)
        where T : DbContext
    {
        var builder = new DbContextOptionsBuilder<T>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                new ElibSaveChangesInterceptor(tenant, new CurrentActor(), TimeProvider.System),
                new TenantRlsConnectionInterceptor(tenant));
        configure?.Invoke(builder);
        return builder.Options;
    }

    private static async Task Exec(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresGroup : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
