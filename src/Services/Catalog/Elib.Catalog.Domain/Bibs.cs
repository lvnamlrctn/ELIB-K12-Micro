using System.Text.RegularExpressions;
using Elib.BuildingBlocks.Domain;

namespace Elib.Catalog.Domain;

/// <summary>
/// Loại biểu ghi (monolith: PrintBook.BibType) — quyết định Leader/06 (dạng tài liệu) và Leader/07 (cấp thư mục),
/// và biểu mẫu biên mục gợi ý.
/// </summary>
public sealed partial class BibType : TenantEntity
{
    private BibType() { }

    public string Name { get; private set; } = "";
    public string Code { get; private set; } = "";

    /// <summary>Leader/06: a = tài liệu ngôn ngữ, e = bản đồ, g = nghe nhìn, m = tệp máy tính, t = bản thảo…</summary>
    public string RecordType { get; private set; } = "a";

    /// <summary>Leader/07: m = chuyên khảo, s = ấn phẩm tiếp tục, a/b = phần của tài liệu…</summary>
    public string BibLevel { get; private set; } = "m";

    public static BibType Create(string name, string code, string recordType, string bibLevel)
    {
        var type = new BibType();
        type.Update(name, code, recordType, bibLevel);
        return type;
    }

    public void Update(string name, string code, string recordType, string bibLevel)
    {
        var n = (name ?? "").Trim();
        Name = n.Length is > 0 and <= 250 ? n : throw new BusinessRuleException("NAME_INVALID", "Tên loại biểu ghi bắt buộc, tối đa 250 ký tự.");
        var c = (code ?? "").Trim().ToUpperInvariant();
        Code = CodePattern().IsMatch(c) ? c : throw new BusinessRuleException("CODE_INVALID", "Mã loại biểu ghi bắt buộc, tối đa 20 ký tự chữ, số, '-', '_'.");
        RecordType = OneOf(recordType, "acdefgijkmoprt", "Dạng tài liệu (Leader/06)");
        BibLevel = OneOf(bibLevel, "abcdims", "Cấp thư mục (Leader/07)");
    }

    private static string OneOf(string? value, string allowed, string label)
    {
        var v = (value ?? "").Trim().ToLowerInvariant();
        return v.Length == 1 && allowed.Contains(v[0], StringComparison.Ordinal)
            ? v
            : throw new BusinessRuleException("BIB_TYPE_LEADER_INVALID", $"{label} phải là một trong: {string.Join(", ", allowed.ToCharArray())}.");
    }

    [GeneratedRegex("^[A-Z0-9_-]{1,20}$")]
    private static partial Regex CodePattern();
}

/// <summary>
/// Biểu mẫu biên mục (monolith: Bib_Worksheet + Worksheet_Field + Worksheet_Subfield) — danh sách trường dựng sẵn,
/// có giá trị mặc định, nạp vào màn biên mục khi tạo biểu ghi mới.
/// </summary>
public sealed class Worksheet : TenantEntity
{
    private Worksheet() { }

    public string Name { get; private set; } = "";
    public long? BibTypeId { get; private set; }
    public List<MarcField> Fields { get; private set; } = [];

    public static Worksheet Create(string name, long? bibTypeId, IEnumerable<MarcField>? fields)
    {
        var sheet = new Worksheet();
        sheet.Update(name, bibTypeId, fields);
        return sheet;
    }

    public void Update(string name, long? bibTypeId, IEnumerable<MarcField>? fields)
    {
        var n = (name ?? "").Trim();
        Name = n.Length is > 0 and <= 250 ? n : throw new BusinessRuleException("NAME_INVALID", "Tên biểu mẫu bắt buộc, tối đa 250 ký tự.");
        BibTypeId = bibTypeId;
        Fields = MarcRecord.Normalize(fields, template: true);
        if (Fields.Count == 0) throw new BusinessRuleException("WORKSHEET_EMPTY", "Biểu mẫu phải có ít nhất một trường.");
    }
}

/// <summary>
/// Biểu ghi thư mục (monolith: Bib + BibData + BibXml + fixed_field_value gộp làm một). MARC lưu nguyên dạng JSON
/// (<see cref="Fields"/>, không gồm Leader/001/005); các cột tóm tắt rút từ MARC mỗi lần lưu để tìm/lọc và gửi event.
/// MFN = <see cref="Entity.Id"/> như quy ước của monolith. Trạng thái: 2 = hiện trên OPAC, 1 = ẩn.
/// <see cref="Version"/> tăng ở mỗi thay đổi — bản sao ở holdings/circulation/search bỏ qua event cũ hơn.
/// </summary>
public sealed class Bib : TenantEntity, IHasStatus
{
    private Bib() { }

    public long? BibTypeId { get; private set; }
    public long? WorksheetId { get; private set; }
    public string Leader { get; private set; } = "";
    public List<MarcField> Fields { get; private set; } = [];

    public string Title { get; private set; } = "";
    public string? Author { get; private set; }
    public string? Publisher { get; private set; }
    public string? PublishYear { get; private set; }

    /// <summary>ISBN đã chuẩn hoá, dạng "|isbn1|isbn2|" — tra trùng bằng Contains("|isbn|") trên mọi CSDL.</summary>
    public string Isbns { get; private set; } = "";

    public string? Ddc { get; private set; }
    public string? Keywords { get; private set; }
    public string? Language { get; private set; }

    /// <summary>Chữ thường không dấu của nhan đề, tác giả, NXB, từ khoá, ISBN, DDC — cho tìm kiếm ở màn quản trị.</summary>
    public string SearchText { get; private set; } = "";

    public int Status { get; private set; } = IHasStatus.Active;
    public long Version { get; private set; }

    public IReadOnlyList<string> IsbnList => Isbns.Split('|', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Biểu ghi mới. <paramref name="type"/> null → Leader mặc định của sách (a/m).</summary>
    public static Bib Create(BibType? type, long? worksheetId, string? leader, IEnumerable<MarcField> fields, DateOnly today, string? agencyCode)
    {
        var bib = new Bib { WorksheetId = worksheetId };
        bib.Apply(type, leader, fields, today, agencyCode);
        return bib;
    }

    public void Update(BibType? type, string? leader, IEnumerable<MarcField> fields, DateOnly today, string? agencyCode)
    {
        Apply(type, leader, fields, today, agencyCode);
        Version++;
    }

    public void ChangeStatus(int status)
    {
        Status = StatusRules.Validate(status);
        Version++;
    }

    private void Apply(BibType? type, string? leader, IEnumerable<MarcField> fields, DateOnly today, string? agencyCode)
    {
        BibTypeId = type?.Id;
        Leader = MarcRecord.ApplyType(leader?.Trim(), (type?.RecordType ?? "a")[0], (type?.BibLevel ?? "m")[0]);
        var normalized = MarcRecord.Normalize(fields);
        var summary = MarcRecord.Summarize(normalized);

        // 003 (mã cơ quan) và 008 tự sinh khi thiếu — như monolith; giá trị người dùng nhập được giữ nguyên.
        if (!normalized.Any(f => f.Tag == "003") && !string.IsNullOrWhiteSpace(agencyCode))
            normalized.Add(new MarcField("003", Value: agencyCode));
        if (!normalized.Any(f => f.Tag == "008"))
            normalized.Add(new MarcField("008", Value: MarcRecord.BuildField008(today, summary.PublishYear)));
        Fields = [.. normalized.OrderBy(f => f.Tag, StringComparer.Ordinal)];

        Title = summary.Title;
        Author = summary.Author;
        Publisher = summary.Publisher;
        PublishYear = summary.PublishYear;
        Isbns = summary.Isbns.Count > 0 ? "|" + string.Join('|', summary.Isbns) + "|" : "";
        Ddc = summary.Ddc;
        Keywords = summary.Keywords;
        Language = summary.Language;
        var search = MarcRecord.Fold(string.Join(' ', Title, Author, Publisher, Keywords, Ddc, string.Join(' ', summary.Isbns)));
        SearchText = search.Length <= 4000 ? search : search[..4000];
    }
}
