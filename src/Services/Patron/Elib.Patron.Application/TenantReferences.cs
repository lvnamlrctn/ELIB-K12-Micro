using Elib.BuildingBlocks.Domain;

namespace Elib.Patron.Application;

/// <summary>Một mục danh mục của service tenant (ParentId: cây phòng ban/đơn vị).</summary>
public sealed record ReferenceItem(long Id, string Name, long? ParentId = null);

/// <summary>
/// Danh mục tham chiếu của đơn vị hiện tại ở service tenant (Đơn vị, Dân tộc, Trình độ, Học hàm) — patron chỉ giữ id; nhập/xuất Excel cần
/// tên. Gọi /internal/tenants/{id}/reference/{kind} bằng service token.
/// </summary>
public interface ITenantReferences
{
    Task<IReadOnlyList<ReferenceItem>> ListAsync(string kind, CancellationToken ct);
}

public static class ReferenceKinds
{
    public const string Orgs = "orgs";
    public const string Ethnicities = "ethnicities";
    public const string Degrees = "degrees";
    public const string AcademicTitles = "academic-titles";
}

/// <summary>Danh mục đã tải trong một lượt nhập/xuất: tên theo id, id theo tên (không dấu cách thừa, không phân biệt hoa thường).</summary>
public sealed class ReferenceLookup(IReadOnlyList<ReferenceItem> items)
{
    private readonly Dictionary<long, string> _names = items.ToDictionary(i => i.Id, i => i.Name);
    private readonly ILookup<string, long> _ids = items.ToLookup(i => Key(i.Name), i => i.Id);

    public string? Name(long? id) => id is { } v ? _names.GetValueOrDefault(v) : null;

    /// <summary>Tên (duy nhất) hoặc số id của mục (như tuỳ chọn "theo mã" khi nhập của ELIB-LRC). Trống → null.</summary>
    public long? Resolve(string? text, string field)
    {
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value)) return null;
        if (long.TryParse(value, out var id) && _names.ContainsKey(id)) return id;
        return _ids[Key(value)].ToList() switch
        {
            [var only] => only,
            [] => throw new BusinessRuleException("IMPORT_REF_NOT_FOUND", $"{field} '{value}' không có trong danh mục."),
            _ => throw new BusinessRuleException("IMPORT_REF_AMBIGUOUS", $"{field} '{value}' trùng tên nhiều mục — ghi số id của mục thay cho tên."),
        };
    }

    /// <summary>Chỉ nhận số id có trong danh mục (nhập "theo mã" của ELIB-LRC — ô chứa thẳng id, không tra tên). Trống → null.</summary>
    public long? ResolveId(string? text, string field)
    {
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value)) return null;
        return long.TryParse(value, out var id) && _names.ContainsKey(id) ? id
            : throw new BusinessRuleException("IMPORT_REF_NOT_FOUND", $"{field} mã '{value}' không có trong danh mục.");
    }

    private static string Key(string name) => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
}
