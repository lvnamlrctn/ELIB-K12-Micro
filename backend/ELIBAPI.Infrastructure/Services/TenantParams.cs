using System.Text.RegularExpressions;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Đọc SystemParameter theo đơn vị: dòng của đơn vị trước, không có thì dòng dùng chung (TenantId null).
/// tenantId null chỉ đọc dòng dùng chung — khác ISystemParameterService.GetValueAsync(code, null) (lấy dòng của bất kỳ đơn vị
/// nào), vì job/webhook chạy ngoài phiên đăng nhập và phải đọc đúng cấu hình của đơn vị sở hữu bản ghi.</summary>
public static partial class TenantParams
{
    public static async Task<string?> ValueAsync(ELIBAPIDbContext db, string code, long? tenantId)
    {
        var lower = code.ToLower();
        return await db.SystemParameters.AsNoTracking()
            .Where(x => x.IsDelete != 2 && x.Code!.ToLower() == lower && (x.TenantId == null || x.TenantId == tenantId))
            .OrderByDescending(x => x.TenantId != null).ThenByDescending(x => x.Id)
            .Select(x => x.Value).FirstOrDefaultAsync();
    }

    /// <summary>Giá trị đã bỏ thẻ HTML (trang tham số dùng trình soạn thảo) và khoảng trắng; null/rỗng → "".</summary>
    public static async Task<string> PlainAsync(ELIBAPIDbContext db, string code, long? tenantId) =>
        HtmlTag().Replace(await ValueAsync(db, code, tenantId) ?? "", "").Trim();

    /// <summary>Cờ bật/tắt ("1").</summary>
    public static async Task<bool> IsEnabledAsync(ELIBAPIDbContext db, string code, long? tenantId) =>
        await PlainAsync(db, code, tenantId) == "1";

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTag();
}
