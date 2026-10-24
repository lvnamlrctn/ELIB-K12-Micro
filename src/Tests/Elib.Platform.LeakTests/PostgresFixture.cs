using Elib.Acquisition.Infrastructure;
using Elib.Serials.Infrastructure;
using Elib.Audit.Infrastructure;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.Identity.Infrastructure;
using Elib.Media.Infrastructure;
using Elib.Notification.Infrastructure;
using Elib.Catalog.Infrastructure;
using Elib.Circulation.Infrastructure;
using Elib.Digital.Infrastructure;
using Elib.Payment.Infrastructure;
using Elib.Portal.Infrastructure;
using Elib.Space.Infrastructure;
using Elib.Holdings.Infrastructure;
using Elib.Search.Infrastructure;
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
        new("identity", "elib_leak_identity", ["staff_users", "roles", "reader_accounts"],
            (cs, t) => new IdentityDbContext(Options<IdentityDbContext>(cs, t, o => o.UseOpenIddict()), t)),
        new("tenant", "elib_leak_tenant", ["system_parameters", "orgs", "tenant_module_licenses"],
            (cs, t) => new TenantDbContext(Options<TenantDbContext>(cs, t), t)),
        new("notification", "elib_leak_notification", ["email_settings", "email_templates", "notification_logs"],
            (cs, t) => new NotificationDbContext(Options<NotificationDbContext>(cs, t), t)),
        new("audit", "elib_leak_audit", ["audit_logs"],
            (cs, t) => new AuditDbContext(Options<AuditDbContext>(cs, t), t)),
        new("media", "elib_leak_media", ["media_files"],
            (cs, t) => new MediaDbContext(Options<MediaDbContext>(cs, t), t)),
        new("patron", "elib_leak_patron", ["readers", "reader_types", "classes", "courses", "reader_groups", "reader_import_jobs", "reader_group_members", "card_fees"],
            (cs, t) => new PatronDbContext(Options<PatronDbContext>(cs, t), t)),
        new("catalog", "elib_leak_catalog", ["bibs", "bib_types", "worksheets", "dictionary_terms", "isbd_configs"],
            (cs, t) => new CatalogDbContext(Options<CatalogDbContext>(cs, t), t)),
        new("holdings", "elib_leak_holdings", ["store_types", "stores", "items", "bib_snapshots", "shelf_replicas", "shelf_row_replicas", "floor_replicas", "item_movements", "change_requests", "approval_rules"],
            (cs, t) => new HoldingsDbContext(Options<HoldingsDbContext>(cs, t), t)),
        new("acquisition", "elib_leak_acquisition", ["sources", "suppliers", "budgets", "funds", "bib_snapshots", "purchase_orders", "order_lines",
                "receipts", "receipt_lines"],
            (cs, t) => new AcquisitionDbContext(Options<AcquisitionDbContext>(cs, t), t)),
        new("serials", "elib_leak_serials", ["publication_types", "frequencies", "numbering_patterns", "subscriptions", "serial_issues", "bindings", "binding_issues", "bib_snapshots"],
            (cs, t) => new SerialsDbContext(Options<SerialsDbContext>(cs, t), t)),
        new("circulation", "elib_leak_circulation", ["circ_places", "loan_policies", "loans", "patron_replicas", "item_replicas", "bib_snapshots",
                "fine_reasons", "fine_tickets", "fine_lines", "holds", "loan_renewals", "photocopies", "sip2_stations", "sip2_logs", "library_visits", "fine_payments", "change_requests", "approval_rules"],
            (cs, t) => new CirculationDbContext(Options<CirculationDbContext>(cs, t), t)),
        new("search", "elib_leak_search", ["search_bibs", "search_items", "search_ebooks", "search_sync_states", "search_queries", "z3950_servers"],
            (cs, t) => new SearchDbContext(Options<SearchDbContext>(cs, t), t)),
        new("digital", "elib_leak_digital", ["ebook_collections", "ebook_subjects", "ebook_topics", "ebook_dig_types", "ebook_items",
                "ebook_files", "policy_digitals", "policy_digital_by_collections", "ebook_item_loans", "ebook_item_reservations", "patron_replicas",
                "ebook_reviews", "ebook_favorites", "document_submissions", "metadata_schemas", "metadata_fields", "ebook_metadata_values", "ebook_read_sessions",
                "ebook_read_progress", "ebook_bookmarks", "ebook_access_logs"],
            (cs, t) => new DigitalDbContext(Options<DigitalDbContext>(cs, t), t)),
        new("space", "elib_leak_space", ["map_buildings", "map_floors", "bookable_spaces", "patron_replicas",
                "room_opening_hours", "room_special_days", "space_booking_settings", "room_bookings", "room_booking_members", "room_booking_bans",
                "access_devices", "access_staff_cards", "access_commands", "access_scan_logs", "shelf_rows"],
            (cs, t) => new SpaceDbContext(Options<SpaceDbContext>(cs, t), t)),
        new("payment", "elib_leak_payment", ["payment_accounts", "charges", "charge_sync_states", "payment_transactions", "webhook_logs"],
            (cs, t) => new PaymentDbContext(Options<PaymentDbContext>(cs, t), t)),
        new("portal", "elib_leak_portal", ["news_categories", "news_events", "news", "news_attachments", "news_comments", "portal_pages", "menu_types", "menus", "link_groups", "portal_links",
                "banners", "ads_groups", "ads", "photo_albums", "photos", "videos", "contact_groups", "contacts", "counter_days"],
            (cs, t) => new PortalDbContext(Options<PortalDbContext>(cs, t), t)),
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
