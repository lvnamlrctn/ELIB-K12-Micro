using System.Text.RegularExpressions;
using Elib.BuildingBlocks.Domain;

namespace Elib.Portal.Domain;

internal static partial class TextRules
{
    public static string? Optional(string? value, int max, string code, string label)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v.Length <= max ? v : throw new BusinessRuleException(code, $"{label} tối đa {max} ký tự.");
    }

    public static string Required(string? value, int max, string code, string label)
    {
        var v = (value ?? "").Trim();
        return v.Length is > 0 && v.Length <= max ? v : throw new BusinessRuleException(code, $"{label} bắt buộc, tối đa {max} ký tự.");
    }

    /// <summary>Ngôn ngữ nội dung: null/trống = hiện ở mọi ngôn ngữ; ngược lại mã 2–5 ký tự (vi, en…).</summary>
    public static string? Language(string? value)
    {
        var v = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length is >= 2 and <= 5 && v.All(c => char.IsAsciiLetter(c) || c == '-') ? v : throw new BusinessRuleException("LANGUAGE_INVALID", "Ngôn ngữ không hợp lệ (vi, en…).");
    }

    /// <summary>Slug URL: chữ thường không dấu, số, gạch ngang.</summary>
    public static string Slug(string? value, string label)
    {
        var v = (value ?? "").Trim().ToLowerInvariant();
        return v.Length is > 0 and <= 150 && SlugPattern().IsMatch(v)
            ? v : throw new BusinessRuleException("SLUG_INVALID", $"{label} chỉ gồm chữ thường không dấu, số và dấu gạch ngang (tối đa 150 ký tự).");
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}

/// <summary>Chữ thường, bỏ dấu tiếng Việt (đ → d), ký tự khác chữ/số thành khoảng trắng đơn — cột tìm kiếm không dấu (như Search's TextFold).</summary>
public static class TextFold
{
    public static string Fold(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            builder.Append(ch is 'đ' or 'Đ' ? 'd' : char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
        }
        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static IReadOnlyList<string> Tokens(string? query, int max = 8) => [.. Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().Take(max)];
}

/// <summary>Tạo slug từ tiêu đề tiếng Việt: bỏ dấu (đ → d), chữ thường, mọi ký tự khác chữ/số thành dấu gạch ngang.</summary>
public static class Slugger
{
    public static string From(string? text, string fallback = "muc")
    {
        var decomposed = (text ?? "").Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            var c = ch is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(ch);
            builder.Append(char.IsAsciiLetterOrDigit(c) ? c : '-');
        }
        var slug = string.Join('-', builder.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (slug.Length > 120) slug = slug[..120].TrimEnd('-');
        return slug.Length == 0 ? fallback : slug;
    }
}

/// <summary>
/// Tệp công khai (ảnh, đính kèm) đã upload qua media — lưu URL đã xác thực thuộc đúng đơn vị/mục đích (như Catalog's Bib.SetCover),
/// tránh phải gọi lại media để tra ngược Guid → URL khi hiển thị trên OPAC.
/// </summary>
public static partial class PortalMedia
{
    public const string ImageCode = "cms-image";
    public const string AttachmentCode = "cms-attachment";
    public const int MaxUrlLength = 1000;

    /// <summary><paramref name="mediaPublicPrefix"/>: tiền tố công khai của media (gateway /s3 → bucket công khai).</summary>
    public static string? Image(string? url, string mediaPublicPrefix, long tenantId, string label) => Validate(url, mediaPublicPrefix, tenantId, ImageCode, ImagePattern(), label);

    public static string? Attachment(string? url, string mediaPublicPrefix, long tenantId, string label) =>
        Validate(url, mediaPublicPrefix, tenantId, AttachmentCode, AttachmentPattern(), label);

    private static string? Validate(string? url, string mediaPublicPrefix, long tenantId, string code, Regex tail, string label)
    {
        var value = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        if (value is null) return null;
        var prefix = $"{(mediaPublicPrefix ?? "").TrimEnd('/')}/{tenantId}/{code}/";
        if (value.Length > MaxUrlLength || value.Any(c => char.IsWhiteSpace(c) || c is '"' or '\'' or '<' or '>' or '\\')
            || !value.StartsWith(prefix, StringComparison.Ordinal) || !tail.IsMatch(value[prefix.Length..]))
            throw new BusinessRuleException("MEDIA_URL_INVALID", $"{label} phải là tệp đã upload của đơn vị này.");
        return value;
    }

    [GeneratedRegex("^[0-9]{4}/[0-9]{2}/[0-9a-f]{32}\\.(png|jpg|webp|gif)$")]
    private static partial Regex ImagePattern();

    [GeneratedRegex("^[0-9]{4}/[0-9]{2}/[0-9a-f]{32}\\.(pdf|doc|docx)$")]
    private static partial Regex AttachmentPattern();
}

/// <summary>Liên kết ra ngoài/đi trong OPAC: chỉ http(s) hoặc đường dẫn nội bộ bắt đầu bằng "/" (không "//") — chặn javascript:, data:, protocol-relative.</summary>
public static class SafeLink
{
    public const int MaxLength = 500;

    public static string? Validate(string? link, string label)
    {
        var v = link?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        var ok = v.Length <= MaxLength && !v.Any(char.IsControl)
            && ((v.StartsWith('/') && !v.StartsWith("//", StringComparison.Ordinal) && !v.Contains('\\', StringComparison.Ordinal))
                || (Uri.TryCreate(v, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && !string.IsNullOrEmpty(uri.Host)));
        return ok ? v : throw new BusinessRuleException("LINK_INVALID", $"{label} chỉ nhận địa chỉ http(s) hoặc đường dẫn nội bộ bắt đầu bằng \"/\".");
    }
}

/// <summary>
/// Danh mục dạng cây dùng chung bởi <see cref="NewsCategory"/>/<see cref="Menu"/> (LRC: ParentId/Level/Order). Level luôn suy ra từ cha, tối đa
/// <see cref="MaxDepth"/> cấp (cùng khuôn Digital's TreeCatalogItem).
/// </summary>
public abstract class PortalTreeItem : TenantEntity, IHasStatus
{
    public const int MaxDepth = 6;

    public string Name { get; private set; } = "";
    public long? ParentId { get; private set; }
    public int Level { get; private set; } = 1;
    public int SortOrder { get; private set; }
    public string? Language { get; private set; }
    public int Status { get; private set; } = IHasStatus.Active;

    protected abstract int MaxNameLength { get; }

    protected void Describe(string? name, string? language)
    {
        Name = TextRules.Required(name, MaxNameLength, "NAME_INVALID", "Tên");
        Language = TextRules.Language(language);
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);

    public void Reorder(int sortOrder) => SortOrder = sortOrder;

    public void AttachTo(PortalTreeItem? parent)
    {
        if (parent is not null && parent.Id == Id) throw new BusinessRuleException("TREE_PARENT_INVALID", "Không chọn chính mục này làm mục cha.");
        ParentId = parent?.Id;
        Level = (parent?.Level ?? 0) + 1;
        if (Level > MaxDepth) throw new BusinessRuleException("TREE_TOO_DEEP", $"Cây tối đa {MaxDepth} cấp.");
    }

    public void ShiftLevel(int delta)
    {
        Level += delta;
        if (Level > MaxDepth) throw new BusinessRuleException("TREE_TOO_DEEP", $"Cây tối đa {MaxDepth} cấp.");
    }
}
