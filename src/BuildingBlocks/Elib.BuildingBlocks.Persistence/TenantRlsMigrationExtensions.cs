using Microsoft.EntityFrameworkCore.Migrations;

namespace Elib.BuildingBlocks.Persistence;

/// <summary>
/// Bật Row-Level Security cho bảng có cột tenant_id — gọi trong migration ngay sau CreateTable.
/// Policy cho phép: đúng đơn vị hiện tại; phạm vi đọc chéo (chỉ SELECT); hoặc ngữ cảnh hệ thống.
/// FORCE áp dụng cả với role sở hữu bảng: seed dữ liệu trong migration phải chạy với app.tenant_system = 'on'.
/// </summary>
public static class TenantRlsMigrationExtensions
{
    public const string PolicyName = "elib_tenant_isolation";

    public static void EnableTenantRls(this MigrationBuilder migrationBuilder, string table, string schema = "public", string column = "tenant_id")
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(BuildEnableSql(table, schema, column));
    }

    public static void DisableTenantRls(this MigrationBuilder migrationBuilder, string table, string schema = "public")
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        var target = Qualified(schema, table);
        migrationBuilder.Sql($"DROP POLICY IF EXISTS {PolicyName} ON {target};\nALTER TABLE {target} NO FORCE ROW LEVEL SECURITY;\nALTER TABLE {target} DISABLE ROW LEVEL SECURITY;");
    }

    public static string BuildEnableSql(string table, string schema = "public", string column = "tenant_id")
    {
        var target = Qualified(schema, table);
        var col = Quote(column);
        const string system = "current_setting('app.tenant_system', true) = 'on'";
        const string current = "NULLIF(current_setting('app.tenant_id', true), '')::bigint";
        const string scope = "string_to_array(NULLIF(current_setting('app.tenant_scope', true), ''), ',')::bigint[]";

        return $"""
            ALTER TABLE {target} ENABLE ROW LEVEL SECURITY;
            ALTER TABLE {target} FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS {PolicyName} ON {target};
            CREATE POLICY {PolicyName} ON {target}
                USING ({system} OR {col} = {current} OR {col} = ANY ({scope}))
                WITH CHECK ({system} OR {col} = {current});
            """;
    }

    private static string Qualified(string schema, string table) => $"{Quote(schema)}.{Quote(table)}";

    private static string Quote(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
