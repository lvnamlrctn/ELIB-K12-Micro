namespace Elib.BuildingBlocks.Tenancy;

/// <summary>
/// Đơn vị (tenant) của lượt xử lý hiện tại — request, consumer hoặc job. Scoped theo DI scope.
/// Mặc định là "chưa xác định": mọi truy vấn dữ liệu có TenantId trả rỗng và mọi lệnh ghi bị từ chối (fail-closed).
/// </summary>
public interface ITenantContext
{
    /// <summary>Đơn vị đang thao tác. null khi chưa xác định hoặc đang ở ngữ cảnh hệ thống.</summary>
    long? TenantId { get; }

    /// <summary>Phạm vi đọc chéo đang bật (qua <see cref="ReadAcross"/>). null = chỉ đọc đơn vị hiện tại.</summary>
    IReadOnlyList<long>? ReadScope { get; }

    /// <summary>Các đơn vị token cho phép đọc chéo (claim tenant_scope). Rỗng nếu không có quyền.</summary>
    IReadOnlyList<long> AllowedReadScope { get; }

    /// <summary>Ngữ cảnh hệ thống (super-admin, saga khởi tạo đơn vị…). Chỉ có hiệu lực khi code bỏ query filter tường minh.</summary>
    bool IsSystem { get; }

    /// <summary>Trả về TenantId hoặc ném <see cref="TenantRequiredException"/>.</summary>
    long RequireTenantId();

    /// <summary>Chuyển sang một đơn vị cụ thể (consumer, job theo tenant). Dispose để khôi phục ngữ cảnh trước.</summary>
    IDisposable Use(long tenantId);

    /// <summary>Chuyển sang ngữ cảnh hệ thống. Dispose để khôi phục.</summary>
    IDisposable UseSystem();

    /// <summary>
    /// Bật đọc chéo trong phạm vi <paramref name="scope"/> (phải nằm trong <see cref="AllowedReadScope"/>, trừ khi đang ở ngữ cảnh hệ thống).
    /// Trong lúc bật, mọi lệnh ghi bị từ chối.
    /// </summary>
    IDisposable ReadAcross(IReadOnlyList<long> scope);
}

public sealed class TenantRequiredException(string message) : InvalidOperationException(message)
{
    public TenantRequiredException() : this("Thao tác cần xác định đơn vị (tenant) nhưng ngữ cảnh hiện tại không có.") { }
}

public sealed class TenantAccessDeniedException(string message) : InvalidOperationException(message);
