using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Elib.BuildingBlocks.Domain;

namespace Elib.Search.Domain;

/// <summary>
/// Máy chủ Z39.50 / SRU của thư viện khác (monolith: Z3950Config, quyền Z3950_CONFIGS) — tra cứu liên thư viện và sao biểu ghi về biên mục.
/// Có <see cref="SruUrl"/> thì tra qua SRU (HTTP), không thì Z39.50 (TCP, BER). <see cref="ShowOnOpac"/>: bạn đọc tra được trên OPAC.
/// Monolith có thêm bảng nhóm (Z3950Group) — ở đây gộp thành <see cref="GroupName"/>.
/// </summary>
public sealed partial class Z3950Server : TenantEntity, IHasStatus
{
    public static readonly string[] RecordSyntaxes = ["USMARC", "UNIMARC"];

    private Z3950Server() { }

    public string Name { get; private set; } = "";
    public string Host { get; private set; } = "";
    public int Port { get; private set; } = 210;
    public string DatabaseName { get; private set; } = "";

    /// <summary>Khổ mẫu bản ghi yêu cầu máy chủ trả: USMARC (MARC21) hoặc UNIMARC.</summary>
    public string RecordSyntax { get; private set; } = "USMARC";

    public string? UserName { get; private set; }

    /// <summary>Mật khẩu Z39.50 (idPass) — không bao giờ trả ra API.</summary>
    public string? Password { get; private set; }

    public string? SruUrl { get; private set; }
    public string? GroupName { get; private set; }
    public bool ShowOnOpac { get; private set; }
    public int Status { get; private set; } = IHasStatus.Active;

    public static Z3950Server Create(string name, string host, int port, string database, string? recordSyntax, string? userName, string? password,
        string? sruUrl, string? groupName, bool showOnOpac)
    {
        var server = new Z3950Server();
        server.Update(name, host, port, database, recordSyntax, userName, password ?? "", sruUrl, groupName, showOnOpac);
        return server;
    }

    /// <summary><paramref name="password"/> null = giữ mật khẩu cũ, "" = bỏ mật khẩu.</summary>
    public void Update(string name, string host, int port, string database, string? recordSyntax, string? userName, string? password,
        string? sruUrl, string? groupName, bool showOnOpac)
    {
        Name = Required(name, 200, "NAME_INVALID", "Tên thư viện");
        var h = (host ?? "").Trim().ToLowerInvariant();
        Host = h.Length is > 0 and <= 255 && HostPattern().IsMatch(h)
            ? h : throw new BusinessRuleException("Z3950_HOST_INVALID", "Địa chỉ máy chủ không hợp lệ (tên miền hoặc IP, không kèm http:// hay cổng).");
        Port = port is > 0 and <= 65535 ? port : throw new BusinessRuleException("Z3950_PORT_INVALID", "Cổng phải từ 1 đến 65535.");
        DatabaseName = Required(database, 100, "Z3950_DATABASE_INVALID", "Tên cơ sở dữ liệu");
        if (DatabaseName.Any(char.IsWhiteSpace))
            throw new BusinessRuleException("Z3950_DATABASE_INVALID", "Tên cơ sở dữ liệu không chứa khoảng trắng.");
        var syntax = string.IsNullOrWhiteSpace(recordSyntax) ? "USMARC" : recordSyntax.Trim().ToUpperInvariant();
        if (syntax == "MARC21") syntax = "USMARC";
        RecordSyntax = RecordSyntaxes.Contains(syntax)
            ? syntax : throw new BusinessRuleException("Z3950_SYNTAX_INVALID", "Khổ mẫu là USMARC (MARC21) hoặc UNIMARC.");
        UserName = Optional(userName, 100, "Tên đăng nhập");
        if (password is not null) Password = Optional(password, 200, "Mật khẩu");

        var sru = Optional(sruUrl, 500, "Địa chỉ SRU");
        if (sru is not null && !(Uri.TryCreate(sru, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                                 && string.IsNullOrEmpty(uri.UserInfo)))
            throw new BusinessRuleException("Z3950_SRU_INVALID", "Địa chỉ SRU phải là URL http(s) đầy đủ, ví dụ https://lx2.loc.gov/sru/lcdb.");
        SruUrl = sru;
        GroupName = Optional(groupName, 100, "Nhóm");
        ShowOnOpac = showOnOpac;
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);

    private static string Required(string? value, int max, string code, string label)
    {
        var v = (value ?? "").Trim();
        return v.Length is > 0 && v.Length <= max ? v : throw new BusinessRuleException(code, $"{label} bắt buộc, tối đa {max} ký tự.");
    }

    private static string? Optional(string? value, int max, string label)
    {
        var v = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return v is null || v.Length <= max ? v : throw new BusinessRuleException("Z3950_VALUE_TOO_LONG", $"{label} tối đa {max} ký tự.");
    }

    [GeneratedRegex("^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)*$|^[0-9a-f:]+$")]
    private static partial Regex HostPattern();
}

public sealed record Z3950Subfield(string Code, string Value);

/// <summary>Trường MARC của bản ghi lấy về — cùng dạng JSON với trường MARC của catalog (tag, ind1, ind2, value, subfields).</summary>
public sealed record Z3950Field(string Tag, string? Ind1 = null, string? Ind2 = null, string? Value = null, IReadOnlyList<Z3950Subfield>? Subfields = null)
{
    public IEnumerable<string> Values(char code) =>
        (Subfields ?? []).Where(s => s.Code.Length == 1 && s.Code[0] == code).Select(s => s.Value);
}

public sealed partial record Z3950Record(string Leader, IReadOnlyList<Z3950Field> Fields)
{
    /// <summary>Thông tin tóm tắt để hiện danh sách: MARC21 (245/100/260-264/020) hoặc UNIMARC (200/700/210/010).</summary>
    public (string? Title, string? Author, string? Publisher, string? Year, string? Isbn) Summary(bool unimarc)
    {
        string? First(string tag, char code) => Fields.Where(f => f.Tag == tag).SelectMany(f => f.Values(code)).Select(Clean).FirstOrDefault(v => v.Length > 0);
        string? Join(string tag, params char[] codes)
        {
            var field = Fields.FirstOrDefault(f => f.Tag == tag);
            var text = field is null ? "" : string.Join(" ", (field.Subfields ?? []).Where(s => s.Code.Length == 1 && codes.Contains(s.Code[0])).Select(s => Clean(s.Value)).Where(v => v.Length > 0));
            return text.Length > 0 ? text : null;
        }
        string? Year(string? date) => date is null ? null : YearPattern().Match(date) is { Success: true } m ? m.Value : null;

        return unimarc
            ? (Join("200", 'a', 'e'), First("700", 'a') ?? First("701", 'a') ?? First("200", 'f'), First("210", 'c'), Year(First("210", 'd')), First("010", 'a'))
            : (Join("245", 'a', 'b'), First("100", 'a') ?? First("110", 'a') ?? First("700", 'a'), First("264", 'b') ?? First("260", 'b'),
                Year(First("264", 'c') ?? First("260", 'c')), First("020", 'a'));
    }

    private static string Clean(string? value) => (value ?? "").Trim().TrimEnd('/', ':', ';', ',', '=', ' ').Trim();

    [GeneratedRegex("[12][0-9]{3}")]
    private static partial Regex YearPattern();
}

/// <summary>
/// Đọc bản ghi MARC máy chủ ngoài trả về: ISO2709 (Z39.50 Present) và MARCXML (SRU recordSchema=marcxml). Không có Leader/09 = 'a' mà
/// dữ liệu là UTF-8 hợp lệ vẫn đọc UTF-8 (như catalog); còn lại đọc Latin-1 — MARC-8 (thư viện nước ngoài) có thể sai dấu.
/// </summary>
public static class Z3950Marc
{
    private const byte RecordTerminator = 0x1D;
    private const byte FieldTerminator = 0x1E;
    private const byte SubfieldDelimiter = 0x1F;
    private const string MarcXmlNamespace = "http://www.loc.gov/MARC21/slim";
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static bool IsControlTag(string tag) => tag.Length == 3 && string.CompareOrdinal(tag, "010") < 0;

    /// <summary>Một bản ghi ISO2709 (đủ Leader); null nếu không đọc được.</summary>
    public static Z3950Record? ReadIso2709(ReadOnlySpan<byte> data)
    {
        var end = data.IndexOf(RecordTerminator);
        if (end >= 0) data = data[..(end + 1)];
        if (data.Length < 25) return null;
        var leader = Encoding.ASCII.GetString(data[..24]);
        if (!int.TryParse(leader.AsSpan(12, 5), NumberStyles.None, CultureInfo.InvariantCulture, out var baseAddress) || baseAddress < 25 || baseAddress > data.Length)
            return null;
        var encoding = leader[9] == 'a' || IsUtf8(data) ? Encoding.UTF8 : Encoding.Latin1;
        var directory = data[24..(baseAddress - 1)];
        var body = data[baseAddress..];
        var fields = new List<Z3950Field>();
        for (var i = 0; i + 12 <= directory.Length; i += 12)
        {
            var entry = Encoding.ASCII.GetString(directory.Slice(i, 12));
            if (!int.TryParse(entry.AsSpan(3, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var length)
                || !int.TryParse(entry.AsSpan(7, 5), NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
                || length < 1 || offset + length > body.Length)
                continue;
            var tag = entry[..3];
            var value = body.Slice(offset, length);
            if (value.Length > 0 && value[^1] == FieldTerminator) value = value[..^1];
            if (IsControlTag(tag))
            {
                fields.Add(new Z3950Field(tag, Value: encoding.GetString(value)));
                continue;
            }
            if (value.Length < 2) continue;
            var subfields = new List<Z3950Subfield>();
            foreach (var piece in encoding.GetString(value[2..]).Split((char)SubfieldDelimiter, StringSplitOptions.RemoveEmptyEntries))
                if (piece.Length > 1 || (piece.Length == 1 && char.IsLetterOrDigit(piece[0]))) subfields.Add(new Z3950Subfield(piece[..1], piece[1..]));
            if (subfields.Count > 0) fields.Add(new Z3950Field(tag, Indicator((char)value[0]), Indicator((char)value[1]), Subfields: subfields));
        }
        return new Z3950Record(leader, fields);
    }

    /// <summary>Các bản ghi &lt;record&gt; MARCXML trong tài liệu SRU (bỏ qua bản ghi không phải MARCXML). Cấm DTD.</summary>
    public static List<Z3950Record> ReadMarcXml(string xml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true };
        using var reader = XmlReader.Create(new StringReader(xml), settings);
        XNamespace ns = MarcXmlNamespace;
        return [.. XDocument.Load(reader).Descendants(ns + "record").Select(r =>
        {
            var leader = (string?)r.Element(ns + "leader") ?? "";
            var fields = new List<Z3950Field>();
            foreach (var e in r.Elements())
            {
                if (e.Name == ns + "controlfield")
                    fields.Add(new Z3950Field((string?)e.Attribute("tag") ?? "", Value: e.Value));
                else if (e.Name == ns + "datafield")
                {
                    var subfields = e.Elements(ns + "subfield").Select(sf => new Z3950Subfield(((string?)sf.Attribute("code") ?? "").Trim(), sf.Value))
                        .Where(sf => sf.Code.Length == 1).ToList();
                    if (subfields.Count > 0)
                        fields.Add(new Z3950Field((string?)e.Attribute("tag") ?? "", Indicator(((string?)e.Attribute("ind1") ?? " ").FirstOrDefault(' ')),
                            Indicator(((string?)e.Attribute("ind2") ?? " ").FirstOrDefault(' ')), Subfields: subfields));
                }
            }
            return new Z3950Record(leader.PadRight(24)[..24], fields);
        })];
    }

    private static string Indicator(char c) => char.IsAsciiLetterOrDigit(c) ? c.ToString() : " ";

    private static bool IsUtf8(ReadOnlySpan<byte> data)
    {
        try
        {
            StrictUtf8.GetCharCount(data);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
