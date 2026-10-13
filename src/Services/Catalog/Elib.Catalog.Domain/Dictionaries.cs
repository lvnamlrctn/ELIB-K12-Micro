using Elib.BuildingBlocks.Domain;

namespace Elib.Catalog.Domain;

/// <summary>Loại từ điển biên mục (monolith: DicAuthor, DicPublisher, DicKeyword, DicClass) và mã quyền tương ứng.</summary>
public static class DictionaryKinds
{
    public const string Author = "author";
    public const string Publisher = "publisher";
    public const string Keyword = "keyword";
    public const string Class = "class";

    public static readonly IReadOnlyList<string> All = [Author, Publisher, Keyword, Class];

    public static string Module(string kind) => kind switch
    {
        Author => "DIC_AUTHORS",
        Publisher => "DIC_PUBLISHERS",
        Keyword => "DIC_KEYWORDS",
        Class => "DIC_CLASSES",
        _ => throw new BusinessRuleException("DICTIONARY_KIND_INVALID", "Loại từ điển không hợp lệ."),
    };
}

/// <summary>
/// Một mục từ điển biên mục của đơn vị: tác giả, nhà xuất bản, từ khoá, ký hiệu phân loại (kèm diễn giải). Cán bộ thêm tay hoặc hệ thống
/// tự học khi lưu biểu ghi (<see cref="AutoAdded"/>); <see cref="UseCount"/> = số biểu ghi đã dùng — gợi ý trong trình biên mục xếp theo
/// mức dùng. Khoá <see cref="Key"/> không dấu, chữ thường, gộp khoảng trắng — "Nguyễn  Nhật Ánh" và "nguyen nhat anh" là một mục.
/// </summary>
public sealed class DictionaryTerm : TenantEntity
{
    public const int MaxValueLength = 500;
    public const int MaxDescriptionLength = 1000;

    private DictionaryTerm() { }

    public string Kind { get; private set; } = "";
    public string Value { get; private set; } = "";
    public string Key { get; private set; } = "";
    public string? Description { get; private set; }
    public int UseCount { get; private set; }
    public bool AutoAdded { get; private set; }

    public static DictionaryTerm Create(string kind, string value, string? description, bool autoAdded = false)
    {
        _ = DictionaryKinds.Module(kind);
        var term = new DictionaryTerm { Kind = kind };
        term.Update(value, description);
        term.AutoAdded = autoAdded;
        return term;
    }

    public void Update(string value, string? description)
    {
        var v = Normalize(value);
        Value = v.Length is > 0 and <= MaxValueLength ? v : throw new BusinessRuleException("DICTIONARY_VALUE_INVALID", $"Giá trị bắt buộc, tối đa {MaxValueLength} ký tự.");
        Key = KeyOf(v);
        var d = description?.Trim();
        Description = string.IsNullOrEmpty(d) ? null
            : d.Length <= MaxDescriptionLength ? d : throw new BusinessRuleException("DICTIONARY_DESCRIPTION_INVALID", $"Diễn giải tối đa {MaxDescriptionLength} ký tự.");
        AutoAdded = false;
    }

    public void Used(int times = 1) => UseCount = Math.Max(0, UseCount + times);

    public static string KeyOf(string value) => MarcRecord.Fold(Normalize(value));

    /// <summary>Gộp khoảng trắng, bỏ dấu câu ISBD/MARC cuối giá trị (",", ".", ":", ";", "/") — giữ dấu chấm viết tắt khi có chữ cái trước.</summary>
    public static string Normalize(string? value)
    {
        var v = string.Join(' ', (value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        while (v.Length > 0 && v[^1] is ',' or ':' or ';' or '/' or '=') v = v[..^1].TrimEnd();
        // "Kim Đồng." → "Kim Đồng"; giữ dấu chấm của chữ viết tắt ngắn ở cuối ("H.", "Tp.").
        if (v.EndsWith('.') && v[..^1].Split(' ')[^1].Length > 2) v = v[..^1].TrimEnd();
        return v;
    }
}

/// <summary>Tách giá trị cho từ điển từ các trường MARC của một biểu ghi (tự học khi lưu).</summary>
public static class DictionaryExtractor
{
    public static IReadOnlyList<(string Kind, string Value)> Extract(IEnumerable<MarcField> fields)
    {
        var result = new List<(string, string)>();
        foreach (var f in fields ?? [])
        {
            var kind = f.Tag switch
            {
                "100" or "110" or "700" or "710" => DictionaryKinds.Author,
                "260" or "264" => DictionaryKinds.Publisher,
                "650" or "653" => DictionaryKinds.Keyword,
                "082" => DictionaryKinds.Class,
                _ => null,
            };
            if (kind is null) continue;
            if (f.Tag == "264" && f.Ind2 is not (null or "" or " " or "1")) continue; // 264: chỉ thông tin xuất bản
            var code = kind == DictionaryKinds.Publisher ? 'b' : 'a';
            foreach (var value in f.Values(code))
                if (DictionaryTerm.Normalize(value) is { Length: > 0 and <= DictionaryTerm.MaxValueLength } v) result.Add((kind, v));
        }
        return [.. result.DistinctBy(x => (x.Item1, DictionaryTerm.KeyOf(x.Item2)))];
    }
}

/// <summary>
/// Cấu hình trình bày ISBD cho một loại biểu ghi (monolith: Config_Isbd + isbd_field + isbd_subfield). Quy tắc theo thứ tự:
/// quy tắc trường (<see cref="IsbdRule.Code"/> trống) đặt dấu mở/đóng quanh cả trường; quy tắc trường con đặt dấu mở/đóng quanh giá trị
/// và dấu nối giữa các lần lặp. Loại chưa cấu hình → quy tắc ISBD mặc định (<see cref="Isbd.Build"/>).
/// </summary>
public sealed class IsbdConfig : TenantEntity
{
    public const int MaxRules = 200;

    private IsbdConfig() { }

    public long BibTypeId { get; private set; }
    public List<IsbdRule> Rules { get; private set; } = [];

    public static IsbdConfig Create(long bibTypeId, IEnumerable<IsbdRule>? rules)
    {
        var config = new IsbdConfig();
        config.Update(bibTypeId, rules);
        return config;
    }

    public void Update(long bibTypeId, IEnumerable<IsbdRule>? rules)
    {
        BibTypeId = bibTypeId > 0 ? bibTypeId : throw new BusinessRuleException("BIB_TYPE_REQUIRED", "Chọn loại biểu ghi.");
        var list = (rules ?? []).Select(IsbdRule.Validate).ToList();
        if (list.Count is 0 or > MaxRules) throw new BusinessRuleException("ISBD_RULES_INVALID", $"Cấu hình ISBD có từ 1 đến {MaxRules} quy tắc.");
        if (!list.Any(r => r.Code is null)) throw new BusinessRuleException("ISBD_RULES_INVALID", "Cần ít nhất một quy tắc trường (để trống trường con).");
        Rules = list;
    }
}

public sealed class IsbdRule
{
    public string Tag { get; set; } = "";

    /// <summary>Mã trường con; trống = quy tắc của cả trường.</summary>
    public string? Code { get; set; }

    public string? Prefix { get; set; }
    public string? Suffix { get; set; }

    /// <summary>Dấu nối giữa các lần lặp của trường con (monolith: Nexttp).</summary>
    public string? Separator { get; set; }

    public static IsbdRule Validate(IsbdRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var tag = (rule.Tag ?? "").Trim();
        if (tag.Length != 3 || !tag.All(char.IsAsciiDigit) || MarcRecord.IsControlTag(tag))
            throw new BusinessRuleException("ISBD_RULE_INVALID", $"Trường '{tag}' không hợp lệ — nhập 3 chữ số của trường dữ liệu (010–999).");
        var code = string.IsNullOrWhiteSpace(rule.Code) ? null : rule.Code.Trim();
        if (code is not null && (code.Length != 1 || !char.IsAsciiLetterOrDigit(code[0])))
            throw new BusinessRuleException("ISBD_RULE_INVALID", $"Trường con '{code}' của {tag} không hợp lệ — một chữ cái hoặc chữ số.");
        static string? Cut(string? s) => s is null ? null : s.Length <= 20 ? s : throw new BusinessRuleException("ISBD_RULE_INVALID", "Dấu câu tối đa 20 ký tự.");
        return new IsbdRule { Tag = tag, Code = code?.ToLowerInvariant(), Prefix = Cut(rule.Prefix), Suffix = Cut(rule.Suffix), Separator = Cut(rule.Separator) };
    }
}
