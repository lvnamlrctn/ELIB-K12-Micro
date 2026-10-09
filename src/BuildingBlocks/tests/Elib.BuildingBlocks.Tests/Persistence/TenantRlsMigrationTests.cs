using Elib.BuildingBlocks.Persistence;

namespace Elib.BuildingBlocks.Tests.Persistence;

public sealed class TenantRlsMigrationTests
{
    [Fact]
    public void Enable_sql_forces_rls_and_restricts_writes_to_current_tenant()
    {
        var sql = TenantRlsMigrationExtensions.BuildEnableSql("books", "catalog");

        Assert.Contains("ALTER TABLE \"catalog\".\"books\" ENABLE ROW LEVEL SECURITY;", sql, StringComparison.Ordinal);
        Assert.Contains("FORCE ROW LEVEL SECURITY", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE POLICY elib_tenant_isolation", sql, StringComparison.Ordinal);

        // WITH CHECK không cho phạm vi đọc chéo được ghi
        var withCheck = sql[sql.IndexOf("WITH CHECK", StringComparison.Ordinal)..];
        Assert.DoesNotContain("app.tenant_scope", withCheck, StringComparison.Ordinal);
    }

    [Fact]
    public void Identifiers_are_quoted()
    {
        var sql = TenantRlsMigrationExtensions.BuildEnableSql("x\"; DROP TABLE y; --");

        Assert.Contains("\"x\"\"; DROP TABLE y; --\"", sql, StringComparison.Ordinal);
    }
}
