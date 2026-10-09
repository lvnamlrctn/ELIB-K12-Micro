using Elib.BuildingBlocks.Domain;

namespace Elib.Tenant.Domain;

/// <summary>
/// Cơ cấu tổ chức trong đơn vị (khối, tổ, phòng…) dạng cây — monolith: dbo.Org. Bạn đọc/nhân viên gắn vào một nút.
/// Level = độ sâu (gốc = 1), luôn suy ra từ cha; SortOrder = thứ tự trong cùng cha.
/// </summary>
public sealed class Org : TenantEntity, IHasStatus
{
    public const int MaxDepth = 10;

    private Org() { }

    public string Name { get; private set; } = "";
    public long? ParentId { get; private set; }
    public int Level { get; private set; } = 1;
    public int SortOrder { get; private set; }
    public int Status { get; private set; } = IHasStatus.Active;
    public string? Link { get; private set; }

    public static Org Create(string name, Org? parent, int sortOrder, int? status, string? link)
    {
        var org = new Org();
        org.Update(name, sortOrder, status, link);
        org.AttachTo(parent);
        return org;
    }

    public void Update(string name, int sortOrder, int? status, string? link)
    {
        var n = (name ?? "").Trim();
        Name = n.Length is > 0 and <= 300 ? n : throw new BusinessRuleException("NAME_INVALID", "Tên tổ chức bắt buộc, tối đa 300 ký tự.");
        var l = string.IsNullOrWhiteSpace(link) ? null : link.Trim();
        Link = l is null || l.Length <= 200 ? l : throw new BusinessRuleException("ORG_LINK_INVALID", "Liên kết tối đa 200 ký tự.");
        SortOrder = sortOrder;
        if (status is not null) ChangeStatus(status.Value);
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);

    public void Reorder(int sortOrder) => SortOrder = sortOrder;

    /// <summary>
    /// Chuyển sang cha mới. <paramref name="descendantIds"/> = mọi nút con cháu hiện tại — chặn chuyển vào chính nhánh của mình.
    /// Trả về độ lệch Level để cập nhật con cháu.
    /// </summary>
    public int MoveTo(Org? parent, int sortOrder, IReadOnlySet<long> descendantIds)
    {
        if (parent is not null && (parent.Id == Id || descendantIds.Contains(parent.Id)))
            throw new BusinessRuleException("ORG_MOVE_CYCLE", "Không chuyển được tổ chức vào chính nó hoặc nhánh con của nó.");
        var before = Level;
        AttachTo(parent);
        SortOrder = sortOrder;
        return Level - before;
    }

    /// <summary>Con cháu đổi Level theo cha khi cha bị chuyển.</summary>
    public void ShiftLevel(int delta)
    {
        Level += delta;
        if (Level > MaxDepth) throw new BusinessRuleException("ORG_TOO_DEEP", $"Cây tổ chức tối đa {MaxDepth} cấp.");
    }

    private void AttachTo(Org? parent)
    {
        if (parent is not null && parent.TenantId != 0 && TenantId != 0 && parent.TenantId != TenantId)
            throw new BusinessRuleException("ORG_PARENT_INVALID", "Tổ chức cha không thuộc đơn vị này.");
        ParentId = parent?.Id;
        Level = (parent?.Level ?? 0) + 1;
        if (Level > MaxDepth) throw new BusinessRuleException("ORG_TOO_DEEP", $"Cây tổ chức tối đa {MaxDepth} cấp.");
    }
}
