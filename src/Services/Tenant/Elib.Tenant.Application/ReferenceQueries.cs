using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Elib.Tenant.Application;

/// <summary>Một mục danh mục tham chiếu (ParentId chỉ có ở phòng ban/đơn vị — cây).</summary>
public sealed record ReferenceItem(long Id, string Name, long? ParentId = null);

/// <summary>
/// Danh mục tham chiếu của đơn vị cho service khác (patron nhập/xuất Excel bạn đọc: Đơn vị, Dân tộc, Trình độ, Học hàm…) — gọi qua
/// /internal bằng service token, một lần cho mỗi lượt nhập/xuất. Gồm cả mục đã ẩn (bạn đọc cũ vẫn trỏ tới), không gồm mục đã xoá.
/// </summary>
public sealed class ReferenceQueries(ITenantDb db, ITenantContext tenant)
{
    public static readonly IReadOnlyList<string> Kinds = ["orgs", "ethnicities", "degrees", "academic-titles", "nationalities", "positions"];

    public async Task<IReadOnlyList<ReferenceItem>> ListAsync(long tenantId, string kind, CancellationToken ct)
    {
        using var scope = tenant.Use(tenantId);
        return kind switch
        {
            "orgs" => await db.Orgs.AsNoTracking().OrderBy(o => o.Id).Select(o => new ReferenceItem(o.Id, o.Name, o.ParentId)).ToListAsync(ct),
            "ethnicities" => await db.Ethnicities.AsNoTracking().OrderBy(o => o.Id).Select(o => new ReferenceItem(o.Id, o.Name, null)).ToListAsync(ct),
            "degrees" => await db.Degrees.AsNoTracking().OrderBy(o => o.Id).Select(o => new ReferenceItem(o.Id, o.Name, null)).ToListAsync(ct),
            "academic-titles" => await db.AcademicTitles.AsNoTracking().OrderBy(o => o.Id).Select(o => new ReferenceItem(o.Id, o.Name, null)).ToListAsync(ct),
            "nationalities" => await db.Nationalities.AsNoTracking().OrderBy(o => o.Id).Select(o => new ReferenceItem(o.Id, o.Name, null)).ToListAsync(ct),
            "positions" => await db.Positions.AsNoTracking().OrderBy(o => o.Id).Select(o => new ReferenceItem(o.Id, o.Name, null)).ToListAsync(ct),
            _ => throw new BusinessRuleException("REFERENCE_KIND_INVALID", $"Danh mục '{kind}' không có. Dùng: {string.Join(", ", Kinds)}."),
        };
    }
}
