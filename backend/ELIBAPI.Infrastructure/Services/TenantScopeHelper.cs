using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>
/// Đợt 24.4 — lọc theo đơn vị (Thư viện) cho các controller viết tay không đi qua BaseRepository (không
/// có sẵn ApplyTenantFilter/ResolveRequestTenantIdAsync/FillTenantNamesAsync). Mirror đúng logic đó: user
/// thường bị ép theo đơn vị JWT (bỏ qua tenantId FE gửi lên); user đặc quyền (không có TenantId JWT, hoặc
/// RoleCode nằm trong ReadOnlyPolicy:RoleCodes/AdminRoleCodes — xem BaseApiController.IsPrivilegedRole)
/// chọn 1 đơn vị cụ thể thì thấy đơn vị đó + dữ liệu dùng chung (TenantId=null); không chọn thì thấy toàn
/// bộ. Mỗi controller vẫn tự viết mệnh đề .Where theo đúng shape entity của mình (2-4 dòng) — chỉ phần
/// lookup DB lặp lại (Guid -> Id nội bộ, Id -> tên đơn vị) được gom vào đây.
/// </summary>
public static class TenantScopeHelper
{
    /// <summary>Phân giải Guid TenantId (FE gửi lên) thành Id nội bộ. Trả null nếu user không đặc quyền
    /// (giá trị FE gửi bị bỏ qua — ép theo JWT), không chọn đơn vị, hoặc không tìm thấy đơn vị đó.</summary>
    public static async Task<long?> ResolveRequestTenantIdAsync(
        ELIBAPIDbContext db, Guid? tenantPublicId, long? jwtTenantId, bool isPrivileged)
    {
        if (!tenantPublicId.HasValue) return null;
        if (jwtTenantId != null && !isPrivileged) return null;
        return await db.Tenants
            .Where(t => t.PublicId == tenantPublicId.Value && t.IsDelete != 2)
            .Select(t => (long?)t.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>Phạm vi đơn vị đã phân giải cho 1 request — truyền xuống service (không có HttpContext). Dùng trong LINQ:
    /// <c>scope.All || x.TenantId == scope.TenantId || (scope.IncludeShared &amp;&amp; x.TenantId == null)</c>.</summary>
    public static async Task<TenantScope> ResolveScopeAsync(
        ELIBAPIDbContext db, Guid? tenantPublicId, long? jwtTenantId, bool isPrivileged)
    {
        if (!isPrivileged) return new TenantScope(jwtTenantId, IncludeShared: false, All: false);
        var requested = await ResolveRequestTenantIdAsync(db, tenantPublicId, jwtTenantId, isPrivileged);
        return requested.HasValue ? new TenantScope(requested, IncludeShared: true, All: false) : new TenantScope(null, false, All: true);
    }

    /// <summary>Nạp map Id -> Tên đơn vị, dùng để enrich cột "Thư viện" ở các projection thủ công (không
    /// đi qua FillTenantNamesAsync vì không phải TEntity của BaseRepository).</summary>
    public static async Task<Dictionary<long, string?>> GetTenantNamesAsync(ELIBAPIDbContext db, IEnumerable<long> tenantIds)
    {
        var ids = tenantIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<long, string?>();
        return await db.Tenants
            .Where(t => ids.Contains(t.Id) && t.IsDelete != 2)
            .ToDictionaryAsync(t => t.Id, t => t.Name);
    }
}

/// <summary>Phạm vi đơn vị của 1 request (xem <see cref="TenantScopeHelper.ResolveScopeAsync"/>). <c>All</c> = tài khoản đặc
/// quyền không chọn đơn vị (thấy tất cả); <c>IncludeShared</c> = đặc quyền chọn 1 đơn vị (gộp dữ liệu dùng chung);
/// còn lại = user thường, chỉ đúng đơn vị JWT. <see cref="WriteTenantId"/>: đơn vị gắn cho bản ghi mới khi không suy
/// được từ dữ liệu liên quan.</summary>
public readonly record struct TenantScope(long? TenantId, bool IncludeShared, bool All)
{
    public long? WriteTenantId => All ? null : TenantId;
}
