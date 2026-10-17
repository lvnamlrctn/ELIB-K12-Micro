using System.Text.RegularExpressions;
using Elib.BuildingBlocks.Domain;

namespace Elib.Digital.Domain;

/// <summary>
/// Ảnh bìa công khai (bộ sưu tập, tài liệu số) đã upload qua media (mục đích <see cref="Code"/>) — lưu URL đã xác thực thuộc đúng
/// đơn vị/mục đích (như Catalog's Bib.SetCover), tránh phải gọi lại media để tra ngược Guid → URL khi hiển thị trên OPAC.
/// </summary>
public static partial class EbookCover
{
    public const string Code = "ebook-cover";
    public const int MaxUrlLength = 1000;

    /// <summary><paramref name="mediaPublicPrefix"/>: tiền tố công khai của media (gateway /s3 → bucket công khai), xem DigitalOptions.</summary>
    public static string? Validate(string? url, string mediaPublicPrefix, long tenantId)
    {
        var value = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        if (value is null) return null;
        var prefix = $"{(mediaPublicPrefix ?? "").TrimEnd('/')}/{tenantId}/{Code}/";
        if (value.Length > MaxUrlLength || value.Any(c => char.IsWhiteSpace(c) || c is '"' or '\'' or '<' or '>' or '\\')
            || !value.StartsWith(prefix, StringComparison.Ordinal) || !MediaPathPattern().IsMatch(value[prefix.Length..]))
            throw new BusinessRuleException("COVER_URL_INVALID", "Ảnh bìa phải là ảnh đã upload (hoặc lấy về kho) của đơn vị này.");
        return value;
    }

    /// <summary>Phần sau tiền tố của key media: yyyy/MM/{guid N}.{png|jpg|webp}.</summary>
    [GeneratedRegex("^[0-9]{4}/[0-9]{2}/[0-9a-f]{32}\\.(png|jpg|webp)$")]
    private static partial Regex MediaPathPattern();
}
