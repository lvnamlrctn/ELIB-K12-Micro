using System.Data.Common;
using System.Globalization;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Elib.BuildingBlocks.Persistence;

/// <summary>
/// Lớp bảo vệ thứ hai (docs 04 §2.1): mỗi lần mở connection, đặt biến phiên PostgreSQL mà policy RLS đọc
/// (<c>app.tenant_id</c>, <c>app.tenant_scope</c>, <c>app.tenant_system</c>). Npgsql reset trạng thái phiên khi trả connection về pool,
/// nên phải đặt lại ở mỗi lần mở. Đổi ngữ cảnh tenant giữa chừng trong một connection đang mở sẽ KHÔNG cập nhật biến phiên —
/// job/consumer phải dùng DI scope riêng cho mỗi tenant.
/// </summary>
public sealed class TenantRlsConnectionInterceptor(ITenantContext tenant) : DbConnectionInterceptor
{
    internal const string Sql =
        "SELECT set_config('app.tenant_id', @tenant_id, false), " +
        "set_config('app.tenant_scope', @tenant_scope, false), " +
        "set_config('app.tenant_system', @tenant_system, false)";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = Sql;
        Add(command, "tenant_id", tenant.TenantId?.ToString(CultureInfo.InvariantCulture) ?? "");
        Add(command, "tenant_scope", tenant.ReadScope is { } scope ? string.Join(',', scope) : "");
        Add(command, "tenant_system", tenant.IsSystem ? "on" : "off");
        return command;
    }

    private static void Add(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
