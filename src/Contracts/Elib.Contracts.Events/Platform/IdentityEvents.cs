namespace Elib.Contracts.Events.Platform;

/// <summary>Quyền của các tài khoản thay đổi — mọi service xoá cache quyền (thay cho PermissionStamp của monolith).</summary>
public sealed record PermissionChanged : IntegrationEvent
{
    /// <summary>Tài khoản bị ảnh hưởng. Rỗng + <see cref="AllUsersOfTenant"/> = mọi tài khoản của đơn vị (đổi vai trò/module).</summary>
    public IReadOnlyList<long> UserIds { get; init; } = [];

    public bool AllUsersOfTenant { get; init; }
}

/// <summary>Quyền của tài khoản cấp hệ thống (không thuộc đơn vị) thay đổi.</summary>
public sealed record SystemPermissionChanged : SystemEvent
{
    public required IReadOnlyList<long> UserIds { get; init; }
}

public sealed record UserUpserted : IntegrationEvent
{
    public required long UserId { get; init; }
    public required string UserName { get; init; }
    public required string FullName { get; init; }
    public bool IsActive { get; init; } = true;
    public required long SourceVersion { get; init; }
}
