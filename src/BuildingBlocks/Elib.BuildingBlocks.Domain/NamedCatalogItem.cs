namespace Elib.BuildingBlocks.Domain;

/// <summary>
/// Danh mục tham chiếu chỉ có tên (quốc tịch, dân tộc, loại bạn đọc, lớp, khoá…). Mỗi đơn vị một bộ riêng.
/// Bỏ PortalId/Language của monolith (không còn đa cổng trong một đơn vị).
/// </summary>
public abstract class NamedCatalogItem : TenantEntity
{
    public string Name { get; private set; } = "";

    /// <summary>Độ dài tối đa như cột nvarchar tương ứng của monolith.</summary>
    public abstract int MaxNameLength { get; }

    public void Rename(string name)
    {
        var value = (name ?? "").Trim();
        Name = value.Length is > 0 and var length && length <= MaxNameLength
            ? value
            : throw new BusinessRuleException("NAME_INVALID", $"Tên bắt buộc, tối đa {MaxNameLength} ký tự.");
    }

    public static T New<T>(string name) where T : NamedCatalogItem, new()
    {
        var item = new T();
        item.Rename(name);
        return item;
    }
}
