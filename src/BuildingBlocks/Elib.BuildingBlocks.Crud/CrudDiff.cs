using System.Text.Json;
using System.Text.Json.Nodes;

namespace Elib.BuildingBlocks.Crud;

/// <summary>
/// Diff trước/sau của một bản ghi cho nhật ký thao tác (<c>AuditRecorded.ChangesJson</c>): <c>{"trường": [cũ, mới]}</c> theo tên thuộc tính
/// của DTO (camelCase). Bỏ id, thời điểm tạo/sửa và mọi trường nhạy cảm (mật khẩu, token, khoá bí mật, số CCCD) — hợp đồng của event cấm.
/// Lý do sửa (header <see cref="ReasonHeader"/>) nằm ở khoá <see cref="ReasonKey"/>.
/// </summary>
public static class CrudDiff
{
    public const string ReasonHeader = "X-Change-Reason";
    public const string ReasonKey = "_reason";
    public const int MaxReasonLength = 500;
    public const int MaxJsonLength = 8000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly string[] Ignored = ["id", "publicId", "createdAt", "updatedAt", "createdBy", "updatedBy", "version"];
    private static readonly string[] Sensitive = ["password", "token", "secret", "citizenid", "hash"];

    /// <summary>null = không có trường nào đổi.</summary>
    public static JsonObject? Compute<T>(T before, T after, IReadOnlyCollection<string>? ignore = null)
    {
        if (JsonSerializer.SerializeToNode(before, Json) is not JsonObject a || JsonSerializer.SerializeToNode(after, Json) is not JsonObject b)
            return null;
        var result = new JsonObject();
        foreach (var (key, newValue) in b)
        {
            if (Ignored.Contains(key, StringComparer.OrdinalIgnoreCase) || (ignore?.Contains(key, StringComparer.OrdinalIgnoreCase) ?? false)) continue;
            if (Sensitive.Any(s => key.Contains(s, StringComparison.OrdinalIgnoreCase))) continue;
            a.TryGetPropertyValue(key, out var oldValue);
            if (JsonNode.DeepEquals(oldValue, newValue)) continue;
            result[key] = new JsonArray(oldValue?.DeepClone(), newValue?.DeepClone());
        }
        return result.Count == 0 ? null : result;
    }

    /// <summary>Lý do do app gửi (mã hoá URL vì header chỉ nhận ASCII); cắt còn 500 ký tự.</summary>
    public static string? Reason(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return null;
        string text;
        try
        {
            text = Uri.UnescapeDataString(header).Trim();
        }
        catch (UriFormatException)
        {
            text = header.Trim();
        }
        return text.Length == 0 ? null : text.Length <= MaxReasonLength ? text : text[..MaxReasonLength];
    }

    /// <summary>Ghép diff và lý do thành JSON ≤ 8000 ký tự (quá dài: chỉ giữ tên trường, bỏ giá trị).</summary>
    public static string? ToJson(JsonObject? changes, string? reason)
    {
        if (changes is null && reason is null) return null;
        var root = changes?.DeepClone().AsObject() ?? [];
        if (reason is not null) root[ReasonKey] = reason;
        var json = root.ToJsonString(Json);
        if (json.Length <= MaxJsonLength) return json;
        var slim = new JsonObject();
        foreach (var (key, _) in root)
            if (key != ReasonKey) slim[key] = new JsonArray("…", "…");
        if (reason is not null) slim[ReasonKey] = reason;
        json = slim.ToJsonString(Json);
        return json.Length <= MaxJsonLength ? json : null;
    }
}
