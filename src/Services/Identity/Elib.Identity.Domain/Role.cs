using System.Text.RegularExpressions;
using Elib.BuildingBlocks.Domain;

namespace Elib.Identity.Domain;

/// <summary>Vai trò trong một đơn vị: tập mã quyền dạng <c>MODULE:action</c> (giữ ngữ nghĩa [Permission] của monolith) hoặc "*".</summary>
public sealed partial class Role : TenantEntity
{
    public const string AdminRoleName = "Quản trị đơn vị";
    public const string LibrarianRoleName = "Thủ thư";

    private Role() { }

    public static Role Create(string name, string? description, IEnumerable<string> permissions, bool isBuiltIn = false)
    {
        var role = new Role { IsBuiltIn = isBuiltIn };
        role.Update(name, description, permissions);
        return role;
    }

    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public bool IsBuiltIn { get; private set; }
    public List<string> Permissions { get; private set; } = [];

    public void Update(string name, string? description, IEnumerable<string> permissions)
    {
        Name = string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100
            ? throw new BusinessRuleException("ROLE_NAME_INVALID", "Tên vai trò bắt buộc, tối đa 100 ký tự.")
            : name.Trim();
        Description = description?.Trim();
        Permissions = NormalizePermissions(permissions);
    }

    public void EnsureDeletable()
    {
        if (IsBuiltIn) throw new ConflictException("ROLE_BUILT_IN", "Không xoá được vai trò mặc định của hệ thống.");
    }

    public static List<string> NormalizePermissions(IEnumerable<string> permissions)
    {
        var result = new List<string>();
        foreach (var raw in permissions ?? [])
        {
            var value = (raw ?? "").Trim();
            if (value == "*")
            {
                result.Add(value);
                continue;
            }
            var parts = value.Split(':', 2);
            var code = parts.Length == 2 ? $"{parts[0].Trim().ToUpperInvariant()}:{parts[1].Trim().ToLowerInvariant()}" : value;
            if (!CodePattern().IsMatch(code))
                throw new BusinessRuleException("PERMISSION_CODE_INVALID", $"Mã quyền '{raw}' phải có dạng MODULE:action.");
            result.Add(code);
        }
        return result.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }

    [GeneratedRegex("^[A-Z0-9_]{2,40}:[a-z0-9_]{2,30}$")]
    private static partial Regex CodePattern();
}
