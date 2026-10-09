using Elib.BuildingBlocks.Tenancy;
using Elib.Media.Domain;
using Elib.Media.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Elib.Platform.LeakTests;

/// <summary>
/// Tenant-leak test (docs 09, tiêu chí GĐ0): chạy migration thật của mọi service trên PostgreSQL thật, bằng role ứng dụng
/// không phải superuser, rồi kiểm tra lớp bảo vệ thứ hai (RLS) — kể cả khi code quên/bỏ query filter của EF.
/// </summary>
[Collection(PostgresGroup.Name)]
public sealed class TenantLeakTests
{
    private const long TenantA = 7001;
    private const long TenantB = 7002;

    /// <summary>
    /// Bảng có tenant_id được phép KHÔNG có RLS, kèm lý do. Thêm vào đây phải có lý do rõ ràng.
    /// </summary>
    private static readonly Dictionary<string, string> Exempt = new()
    {
        ["tenant.tenant_provisioning_steps"] = "tenant_id là khoá ngoại tới đơn vị đang khởi tạo; chỉ ngữ cảnh hệ thống (saga) ghi/đọc",
    };

    /// <summary>Schema dữ liệu dùng chung, không thuộc đơn vị nào.</summary>
    private static readonly string[] SharedSchemas = ["replica", "masstransit", "information_schema", "pg_catalog"];


    [PostgresFact]
    public async Task App_role_is_not_superuser_and_cannot_bypass_rls()
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.AppConnection(PostgresFixture.Get("media")));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user", connection);
        Assert.False((bool)(await command.ExecuteScalarAsync())!);
    }

    [PostgresFact]
    public async Task Every_table_with_tenant_id_forces_row_level_security()
    {
        var missing = new List<string>();
        foreach (var db in PostgresFixture.Services)
        {
            var rls = await TablesAsync(db, """
                SELECT n.nspname || '.' || c.relname, c.relforcerowsecurity AND p.oid IS NOT NULL
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                JOIN pg_attribute a ON a.attrelid = c.oid AND a.attname = 'tenant_id' AND NOT a.attisdropped
                LEFT JOIN pg_policy p ON p.polrelid = c.oid AND p.polname = 'elib_tenant_isolation'
                WHERE c.relkind IN ('r', 'p') AND n.nspname <> ALL (@shared)
                """);
            foreach (var (table, protectedByRls) in rls)
            {
                if (!protectedByRls && !Exempt.ContainsKey($"{db.Service}.{table.Split('.')[1]}")) missing.Add($"{db.Database}: {table}");
            }
            foreach (var expected in db.ExpectedRlsTables)
                Assert.True(rls.Any(r => r.Table == $"public.{expected}" && r.Rls), $"{db.Service}: bảng {expected} phải có RLS");
        }
        Assert.True(missing.Count == 0, "Bảng có tenant_id nhưng thiếu FORCE RLS + policy elib_tenant_isolation:\n" + string.Join("\n", missing));
    }

    [PostgresFact]
    public async Task Other_tenant_cannot_insert_rows_for_a_tenant_into_any_table()
    {
        // Ghi bằng SQL thô (không qua interceptor của EF) trong ngữ cảnh đơn vị B với tenant_id = A: policy WITH CHECK phải chặn
        // trên MỌI bảng có RLS. PostgreSQL kiểm WITH CHECK của RLS trước ràng buộc NOT NULL nên lỗi phải là 42501.
        var failures = new List<string>();
        foreach (var db in PostgresFixture.Services)
        {
            var tables = (await TablesAsync(db, """
                SELECT n.nspname || '.' || c.relname, true FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                JOIN pg_policy p ON p.polrelid = c.oid AND p.polname = 'elib_tenant_isolation'
                WHERE n.nspname <> ALL (@shared)
                """)).Select(t => t.Table).ToList();
            Assert.NotEmpty(tables);

            var tenant = new TenantContext();
            using var asB = tenant.Use(TenantB);
            await using var context = PostgresFixture.Open(db, tenant);
            foreach (var table in tables)
            {
                var quoted = string.Join('.', table.Split('.').Select(p => $"\"{p}\""));
                try
                {
                    await using var tx = await context.Database.BeginTransactionAsync();
#pragma warning disable EF1002 // tên bảng lấy từ catalog của PostgreSQL, không phải dữ liệu người dùng
                    await context.Database.ExecuteSqlRawAsync($"INSERT INTO {quoted} (tenant_id) VALUES ({TenantA})");
#pragma warning restore EF1002
                    failures.Add($"{db.Service}: {table} — ghi được dữ liệu cho đơn vị khác");
                }
                catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InsufficientPrivilege)
                {
                    // đúng: new row violates row-level security policy
                }
                catch (PostgresException ex)
                {
                    failures.Add($"{db.Service}: {table} — lỗi {ex.SqlState} thay vì 42501 (RLS): {ex.MessageText}");
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [PostgresFact]
    public async Task Rows_of_one_tenant_are_invisible_and_immutable_for_another_even_without_ef_filter()
    {
        var db = PostgresFixture.Get("media");
        var marker = $"leak-{Guid.NewGuid():N}.pdf";

        var tenant = new TenantContext();
        using (tenant.Use(TenantA))
        {
            await using var context = (MediaDbContext)PostgresFixture.Open(db, tenant);
            context.Files.Add(MediaFile.Create(TenantA, MediaPurpose.Attachment, marker, "application/pdf", 10, "media-private", DateTimeOffset.UtcNow));
            await context.SaveChangesAsync();
            Assert.Equal(1, await context.Files.CountAsync(f => f.FileName == marker));
        }

        using (tenant.Use(TenantB))
        {
            await using var context = (MediaDbContext)PostgresFixture.Open(db, tenant);
            // Bỏ query filter của EF (lỗi lập trình giả định) — RLS vẫn phải giấu dữ liệu của A.
            Assert.Equal(0, await context.Files.IgnoreQueryFilters().CountAsync(f => f.FileName == marker));
            Assert.Equal(0, await context.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM media_files WHERE file_name = {marker}").SingleAsync());
            Assert.Equal(0, await context.Database.ExecuteSqlAsync($"UPDATE media_files SET file_name = 'hacked' WHERE file_name = {marker}"));
            Assert.Equal(0, await context.Database.ExecuteSqlAsync($"DELETE FROM media_files WHERE file_name = {marker}"));
        }

        // Không có ngữ cảnh (request chưa xác định đơn vị) → không thấy gì.
        await using (var context = (MediaDbContext)PostgresFixture.Open(db, new TenantContext()))
            Assert.Equal(0, await context.Files.IgnoreQueryFilters().CountAsync(f => f.FileName == marker));

        // Đọc chéo hợp lệ (B được cấp phạm vi gồm A): đọc được nhưng vẫn không ghi được.
        var scoped = new TenantContext();
        scoped.Initialize(TenantB, isSystem: false, allowedReadScope: [TenantA]);
        using (scoped.ReadAcross([TenantA, TenantB]))
        {
            await using var context = (MediaDbContext)PostgresFixture.Open(db, scoped);
            Assert.Equal(1, await context.Files.CountAsync(f => f.FileName == marker));
            // Thấy dòng (USING cho phạm vi đọc) nhưng WITH CHECK chỉ nhận đơn vị hiện tại → lỗi RLS, không âm thầm sửa.
            var denied = await Assert.ThrowsAsync<PostgresException>(() =>
                context.Database.ExecuteSqlAsync($"UPDATE media_files SET file_name = 'hacked' WHERE file_name = {marker}"));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }

        // Ngữ cảnh hệ thống thấy dữ liệu của A (job, saga).
        using (tenant.UseSystem())
        {
            await using var context = (MediaDbContext)PostgresFixture.Open(db, tenant);
            Assert.Equal(1, await context.Files.IgnoreQueryFilters().CountAsync(f => f.FileName == marker));
        }
    }

    private static async Task<List<(string Table, bool Rls)>> TablesAsync(ServiceDatabase db, string sql)
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.AppConnection(db));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("shared", SharedSchemas);
        var result = new List<(string, bool)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add((reader.GetString(0), reader.GetBoolean(1)));
        return result;
    }
}
