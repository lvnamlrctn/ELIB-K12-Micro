using Elib.BuildingBlocks.Domain;

namespace Elib.Identity.Domain;

/// <summary>
/// Một module quyền (thay bảng <c>Module</c> + <c>ModuleRoles</c> của monolith): mã dùng trong <c>[Permission("MODULE","action")]</c>,
/// các action module có, và phân hệ license cần mua (null = chức năng nền tảng, đơn vị nào cũng có).
/// </summary>
public sealed record PermissionModule(string Code, string Name, string Group, IReadOnlyList<string> Actions, string? License = null);

/// <summary>
/// Danh mục quyền của nền tảng — nguồn cho màn phân quyền và để kiểm tra mã quyền khi lưu vai trò.
/// Khi port một service: thêm module quyền của service đó vào <see cref="All"/>, cùng mã với <c>[Permission]</c> ở endpoint.
/// Nhóm (<see cref="PermissionModule.Group"/>) theo menu của app Admin, phân cấp bằng " / ".
/// </summary>
public static class PermissionCatalog
{
    public const string View = "view";
    public const string Add = "add";
    public const string Edit = "edit";
    public const string Delete = "delete";

    private static readonly string[] Crud = [View, Add, Edit, Delete];
    private const string ReaderParams = "Quản lý bạn đọc / Tham số bạn đọc";
    private const string SystemGroup = "Hệ thống";
    private const string NotifyGroup = "Hệ thống / Gửi thông báo";

    public static readonly IReadOnlyList<PermissionModule> All =
    [
        new("NATIONALITIES", "Quốc tịch", ReaderParams, Crud),
        new("PROFS", "Học hàm học vị", ReaderParams, Crud),
        new("ETHNICS", "Dân tộc", ReaderParams, Crud),
        new("DEGREES", "Trình độ", ReaderParams, Crud),
        new("POSITIONS", "Chức vụ", ReaderParams, Crud),
        new("ORGS", "Phòng ban", ReaderParams, Crud),

        new("USER", "Cán bộ quản lý thư viện", SystemGroup, [View, Add, Edit]),
        new("ROLE", "Phân quyền", SystemGroup, Crud),
        new("SYSTEM_PARAMS", "Tham số hệ thống", SystemGroup, Crud),
        new("CURRENCIES", "Tiền tệ", SystemGroup, Crud),
        new("SYSTEM_LOG", "Nhật ký hệ thống", SystemGroup, [View]),

        new("NOTIFICATION_CONFIG", "Cấu hình email", NotifyGroup, [View, Edit, Delete]),
        new("NOTIFICATION_TEMPLATES", "Mẫu email", NotifyGroup, Crud),
        new("NOTIFICATION_LOGS", "Nhật ký gửi tin", NotifyGroup, [View]),
    ];

    /// <summary>Module đơn vị được phân quyền: module nền tảng + module thuộc phân hệ đã mua.</summary>
    public static IReadOnlyList<PermissionModule> Available(IReadOnlyCollection<string> licensedModules, IReadOnlyList<PermissionModule>? catalog = null) =>
        (catalog ?? All).Where(m => m.License is null || licensedModules.Contains(m.License)).ToList();

    /// <summary>
    /// Kiểm tra tập quyền của một vai trò (đã chuẩn hoá bằng <see cref="Role.NormalizePermissions"/>): mỗi mã phải có trong
    /// danh mục, đúng action module hỗ trợ, thuộc phân hệ đơn vị đã mua. "*" chỉ dành cho vai trò quản trị mặc định.
    /// </summary>
    public static void Validate(IEnumerable<string> permissions, IReadOnlyList<PermissionModule> available)
    {
        foreach (var code in permissions)
        {
            if (code == "*")
                throw new BusinessRuleException("PERMISSION_ALL_RESERVED", "Quyền \"toàn quyền\" chỉ dành cho vai trò quản trị đơn vị mặc định.");
            var parts = code.Split(':', 2);
            var module = available.FirstOrDefault(m => m.Code == parts[0]);
            if (module is null || parts.Length != 2 || !module.Actions.Contains(parts[1]))
                throw new BusinessRuleException("PERMISSION_UNKNOWN", $"Mã quyền '{code}' không có trong danh mục quyền của đơn vị.");
        }
    }
}
