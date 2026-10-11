using System.Globalization;
using System.Text;
using Elib.BuildingBlocks.Domain;

namespace Elib.Search.Domain;

/// <summary>Chữ thường, bỏ dấu tiếng Việt (đ → d), gộp khoảng trắng — so khớp tìm kiếm không dấu (như catalog MarcRecord.Fold).</summary>
public static class TextFold
{
    public static string Fold(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(ch is 'đ' or 'Đ' ? 'd' : char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
        }
        return string.Join(' ', builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Các từ của câu truy vấn đã bỏ dấu (tối đa <paramref name="max"/> từ, bỏ trùng).</summary>
    public static IReadOnlyList<string> Tokens(string? query, int max = 10) =>
        [.. Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().Take(max)];

    public static string? Cut(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..max];
}

/// <summary>
/// Biểu ghi trong chỉ mục tra cứu (read model của search, docs 04 §6) — ghi từ BibChanged, chỉ khi Version lớn hơn bản đang có.
/// Các cột *Fold là chữ không dấu để tìm; <see cref="SearchText"/> gộp mọi trường tìm nhanh (chỉ mục trigram ở PostgreSQL).
/// Biểu ghi đã xoá giữ dòng với <see cref="Deleted"/> để event cũ tới muộn không làm sống lại.
/// </summary>
public sealed class SearchBib : Entity, ITenantOwned
{
    public const int OpacVisible = 2;

    public long TenantId { get; set; }
    public Guid BibPublicId { get; set; }
    public long Mfn { get; set; }
    public long? BibTypeId { get; set; }
    public string? MaterialType { get; set; }
    public string Title { get; set; } = "";
    public string? Author { get; set; }
    public string? OtherAuthors { get; set; }
    public string? Publisher { get; set; }
    public string? PublishPlace { get; set; }
    public string? PublishYear { get; set; }

    /// <summary>Năm xuất bản dạng số — lọc khoảng năm, sắp xếp mới nhất.</summary>
    public int? Year { get; set; }

    public string? Isbns { get; set; }
    public string? Ddc { get; set; }
    public string? Cutter { get; set; }
    public string? Keywords { get; set; }
    public string? Language { get; set; }
    public string? Summary { get; set; }
    public string? Edition { get; set; }
    public string? PhysicalDescription { get; set; }
    public string? Series { get; set; }
    public string? CoverUrl { get; set; }
    public int Status { get; set; }
    public bool Deleted { get; set; }
    public long Version { get; set; }

    public string TitleFold { get; set; } = "";
    public string AuthorFold { get; set; } = "";
    public string PublisherFold { get; set; } = "";
    public string KeywordFold { get; set; } = "";

    /// <summary>ISBN không gạch, nối bằng khoảng trắng.</summary>
    public string IsbnKey { get; set; } = "";

    public string SearchText { get; set; } = "";
    public DateTimeOffset IndexedAt { get; set; }

    /// <summary>Lần dựng lại chỉ mục gần nhất đã thấy biểu ghi này — biểu ghi không còn ở catalog thì đánh dấu xoá.</summary>
    public Guid? SyncRun { get; set; }

    public static int? ParseYear(string? year) =>
        int.TryParse(year, NumberStyles.None, CultureInfo.InvariantCulture, out var y) && y is >= 1000 and <= 2999 ? y : null;
}

/// <summary>
/// Bản sách trong chỉ mục (ItemChanged từ holdings) + lượt mượn hiện tại (LoanChanged từ circulation) — OPAC hiện số bản, còn bao
/// nhiêu bản sẵn sàng, kho, hạn trả.
/// </summary>
public sealed class SearchItem : Entity, ITenantOwned
{
    /// <summary>Trạng thái holdings hiện trên OPAC: R sẵn sàng (hoặc đang mượn), I chưa xếp giá (đang xử lý).</summary>
    public static readonly string[] OpacStatuses = ["R", "I"];

    public long TenantId { get; set; }
    public Guid ItemPublicId { get; set; }
    public Guid BibPublicId { get; set; }
    public long Mfn { get; set; }
    public string Barcode { get; set; } = "";
    public string BarcodeKey { get; set; } = "";
    public long? StoreId { get; set; }
    public string? StoreName { get; set; }
    public string Status { get; set; } = "";
    public bool Deleted { get; set; }
    public long Version { get; set; }

    public Guid? LoanPublicId { get; set; }
    public long LoanVersion { get; set; }
    public DateTimeOffset? LoanedAt { get; set; }
    public DateTimeOffset? LoanDueAt { get; set; }
    public bool OnLoan { get; set; }
    public Guid? SyncRun { get; set; }

    /// <summary>
    /// Lượt mượn từ circulation — cùng lượt chỉ nhận Version lớn hơn, lượt khác chỉ nhận lượt mượn sau (event lệch thứ tự không ghi đè).
    /// </summary>
    public bool ApplyLoan(Guid loanPublicId, long version, DateTimeOffset loanedAt, DateTimeOffset dueAt, bool returned)
    {
        if (LoanPublicId == loanPublicId ? version <= LoanVersion : LoanedAt is { } current && loanedAt <= current) return false;
        LoanPublicId = loanPublicId;
        LoanVersion = version;
        LoanedAt = loanedAt;
        LoanDueAt = dueAt;
        OnLoan = !returned;
        return true;
    }

    public static string Key(string barcode) => (barcode ?? "").Trim().ToUpperInvariant();
}

/// <summary>Trạng thái dựng chỉ mục của đơn vị (lần dựng lại gần nhất) — màn quản trị "Chỉ mục tra cứu".</summary>
public sealed class SearchSyncState : ITenantOwned
{
    public const string Running = "Running";
    public const string Done = "Done";
    public const string Failed = "Failed";

    public long TenantId { get; set; }
    public string Status { get; set; } = Done;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int Bibs { get; set; }
    public int Items { get; set; }
    public int Loans { get; set; }
    public string? Error { get; set; }
}
