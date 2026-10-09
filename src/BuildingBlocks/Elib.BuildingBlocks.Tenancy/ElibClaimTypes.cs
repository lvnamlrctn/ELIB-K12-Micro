namespace Elib.BuildingBlocks.Tenancy;

/// <summary>Tên claim chuẩn trong access token do identity phát (xem docs 05 §1).</summary>
public static class ElibClaimTypes
{
    public const string Subject = "sub";

    /// <summary>staff | reader | device | service</summary>
    public const string SubjectType = "sub_type";

    /// <summary>Đơn vị (tenant) của người dùng. Không có = tài khoản cấp hệ thống.</summary>
    public const string TenantId = "tenant_id";

    /// <summary>Danh sách đơn vị được phép ĐỌC chéo (cấp Sở/Phòng), phân tách bằng dấu phẩy hoặc khoảng trắng.</summary>
    public const string TenantScope = "tenant_scope";

    public const string PermissionStamp = "pstamp";

    /// <summary>
    /// Quản trị nền tảng đang "đóng vai đơn vị" (docs 04 §2.2): <c>sub</c> là tài khoản hệ thống, <c>tenant_id</c> là đơn vị đang xem.
    /// Giá trị <see cref="ImpersonationModes.ReadOnly"/> = chỉ được dùng quyền "view" — mọi quyền thêm/sửa/xoá bị từ chối.
    /// </summary>
    public const string Impersonation = "imp";
}

public static class ImpersonationModes
{
    public const string ReadOnly = "readonly";
}

public static class ElibSubjectTypes
{
    public const string Staff = "staff";
    public const string Reader = "reader";
    public const string Device = "device";
    public const string Service = "service";
}
