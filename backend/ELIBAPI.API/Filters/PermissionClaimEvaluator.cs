using System.Security.Claims;
using ELIBAPI.Core.Interfaces;

namespace ELIBAPI.API.Filters;

/// <summary>
/// Giai đoạn 2 RBAC (port ELIB-LRC 09-27, xem backend/docs/AUTH_PERMISSION.md): thử quyết định (moduleCode, action) từ
/// claim JWT "perm"/"pstamp"/"ro" thay cho 3–5 truy vấn có join của <see cref="IPermissionService.HasPermissionAsync"/>.
/// Chỉ đúng cho request của CHÍNH chủ token (userId lấy từ NameIdentifier của request hiện tại), KHÔNG dùng để tra quyền
/// hộ user khác. Trả null bất cứ khi nào không chắc chắn (thiếu claim, PermissionStamp lệch so với DB, mã quyền không có
/// trong claim, lỗi parse) — caller PHẢI fallback về <see cref="IPermissionService.HasPermissionAsync"/>.
/// <para>Khác LRC: mã không có trong claim trả null (không phải false) — claim K12 bỏ qua các mã bị trùng dòng module/quyền
/// (xem PermissionService.GetUserPermissionsAsync), các mã đó đi đường DB như trước.</para>
/// </summary>
public static class PermissionClaimEvaluator
{
    public static async Task<bool?> TryEvaluateAsync(ClaimsPrincipal user, IPermissionService permService,
        long userId, string moduleCode, string action)
    {
        var permClaim  = user.FindFirstValue("perm");
        var stampClaim = user.FindFirstValue("pstamp");
        if (permClaim == null || stampClaim == null) return null;

        try
        {
            var currentStamp = await permService.GetPermissionStampAsync(userId);
            if (currentStamp == null || currentStamp != stampClaim) return null; // quyền/role đã đổi từ lúc đăng nhập

            if (permClaim == "*") return true; // role toàn quyền — mirror IsAdminRoleAsync bypass trước tiên

            var lowerAction = action.ToLowerInvariant();
            if (lowerAction is "add" or "edit" or "delete" && user.FindFirstValue("ro") == "1")
                return false; // mirror PermissionService: tài khoản chỉ-xem luôn bị chặn ghi

            foreach (var entry in permClaim.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = entry.IndexOf(':');
                if (idx < 0) continue;
                // Phân biệt hoa thường như HasPermissionAsync (so ModuleCode == trên CSDL).
                if (!string.Equals(entry[..idx], moduleCode, StringComparison.Ordinal)) continue;

                var actions = entry[(idx + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries);
                return actions.Contains(lowerAction, StringComparer.OrdinalIgnoreCase);
            }

            return null; // không có trong claim → không chắc (mã trùng/bị bỏ qua) → đường DB
        }
        catch
        {
            return null; // bất kỳ lỗi nào cũng fallback an toàn, không đoán
        }
    }
}
