using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Elib.Search.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elib.Search.Application;

/// <summary>Cấu hình cổng OAI-PMH (<c>OaiPmh:*</c>).</summary>
public sealed class OaiPmhOptions
{
    public const string Section = "OaiPmh";

    public string RepositoryName { get; set; } = "ELIB";
    public string AdminEmail { get; set; } = "admin@example.org";
    public int PageSize { get; set; } = 100;
}

/// <summary>
/// OAI-PMH 2.0 (monolith/ELIB-LRC: <c>OaiPmhController</c>/<c>OaiPmhService</c>) — cổng chuẩn để hệ thống ngoài (thư viện quốc gia, cổng
/// liên thông, cổng tài nguyên mở…) harvest metadata (Dublin Core / MARC21XML) từ mục lục sách in của đơn vị. Mỗi đơn vị có cổng riêng
/// theo host (gateway định tuyến theo subdomain) nên không cần tham số đơn vị trong identifier như monolith (một CSDL nhiều đơn vị).
///
/// GĐ1 chỉ có sách in trong chỉ mục tra cứu (tài liệu số/ebook chưa có ở <c>search</c>) — một "set" duy nhất, <see cref="SetSpec"/>.
/// MARC21XML dựng khung tối giản từ các trường tóm tắt của <see cref="SearchBib"/> — KHÔNG phải MARC gốc đầy đủ (monolith dựng MARC gốc
/// từ catalog cho sách in, chỉ dùng khung tối giản cho ebook); muốn MARC đầy đủ phải hỏi catalog, chưa làm ở đây.
/// Datestamp lấy từ <see cref="SearchBib.IndexedAt"/> (lần đồng bộ gần nhất, ghi mỗi khi <c>BibChanged</c> được áp dụng) — dựng lại toàn
/// bộ chỉ mục sẽ làm mọi datestamp nhảy lên hiện tại; đây là giới hạn đã biết (monolith suy luận sát hơn từ ngày tạo/sửa bản ghi gốc).
/// </summary>
public sealed class OaiPmh(ISearchDb db, IOptions<OaiPmhOptions> options)
{
    private readonly OaiPmhOptions _opts = options.Value;

    private static readonly XNamespace OaiNs = "http://www.openarchives.org/OAI/2.0/";
    private static readonly XNamespace XsiNs = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace OaiDcNs = "http://www.openarchives.org/OAI/2.0/oai_dc/";
    private static readonly XNamespace DcNs = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace MarcNs = "http://www.loc.gov/MARC21/slim";

    private const string DcPrefix = "oai_dc";
    private const string MarcPrefix = "marc21";
    private const string SetSpec = "printbook";
    private static readonly string[] ValidPrefixes = [DcPrefix, MarcPrefix];

    public async Task<XDocument> HandleAsync(
        string baseUrl, string? verb, string? identifier, string? metadataPrefix, string? from, string? until, string? set, string? resumptionToken,
        CancellationToken ct)
    {
        var errors = new List<(string Code, string Msg)>();
        var reqAttrs = new List<XAttribute>();
        XElement? body = null;

        switch (verb)
        {
            case "Identify":
                reqAttrs.Add(new XAttribute("verb", verb));
                body = await IdentifyAsync(baseUrl, ct);
                break;

            case "ListMetadataFormats":
                reqAttrs.Add(new XAttribute("verb", verb));
                body = ListMetadataFormats();
                break;

            case "ListSets":
                reqAttrs.Add(new XAttribute("verb", verb));
                body = ListSets();
                break;

            case "GetRecord":
                reqAttrs.Add(new XAttribute("verb", verb));
                if (!string.IsNullOrEmpty(identifier)) reqAttrs.Add(new XAttribute("identifier", identifier));
                if (!string.IsNullOrEmpty(metadataPrefix)) reqAttrs.Add(new XAttribute("metadataPrefix", metadataPrefix));
                if (string.IsNullOrEmpty(identifier) || string.IsNullOrEmpty(metadataPrefix))
                    errors.Add(("badArgument", "identifier và metadataPrefix là bắt buộc"));
                else if (!ValidPrefixes.Contains(metadataPrefix))
                    errors.Add(("cannotDisseminateFormat", $"Không hỗ trợ metadataPrefix '{metadataPrefix}'"));
                else
                {
                    var item = await GetSingleAsync(identifier, ct);
                    if (item is null) errors.Add(("idDoesNotExist", $"Không tìm thấy bản ghi '{identifier}'"));
                    else body = new XElement(OaiNs + "GetRecord", BuildRecord(item, metadataPrefix));
                }
                break;

            case "ListIdentifiers":
            case "ListRecords":
                reqAttrs.Add(new XAttribute("verb", verb));
                if (!string.IsNullOrEmpty(metadataPrefix)) reqAttrs.Add(new XAttribute("metadataPrefix", metadataPrefix));
                if (!string.IsNullOrEmpty(from)) reqAttrs.Add(new XAttribute("from", from));
                if (!string.IsNullOrEmpty(until)) reqAttrs.Add(new XAttribute("until", until));
                if (!string.IsNullOrEmpty(set)) reqAttrs.Add(new XAttribute("set", set));
                if (!string.IsNullOrEmpty(resumptionToken)) reqAttrs.Add(new XAttribute("resumptionToken", resumptionToken));
                body = await ListAsync(verb, metadataPrefix, from, until, set, resumptionToken, errors, ct);
                break;

            case null or "":
                errors.Add(("badVerb", "Thiếu tham số verb"));
                break;

            default:
                errors.Add(("badVerb", $"Verb không hợp lệ: '{verb}'"));
                break;
        }

        var root = new XElement(OaiNs + "OAI-PMH",
            new XAttribute(XNamespace.Xmlns + "xsi", XsiNs.NamespaceName),
            new XAttribute(XsiNs + "schemaLocation", "http://www.openarchives.org/OAI/2.0/ http://www.openarchives.org/OAI/2.0/OAI-PMH.xsd"),
            new XElement(OaiNs + "responseDate", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)),
            new XElement(OaiNs + "request", reqAttrs, baseUrl));

        if (errors.Count > 0)
            foreach (var (code, msg) in errors) root.Add(new XElement(OaiNs + "error", new XAttribute("code", code), msg));
        else if (body is not null) root.Add(body);

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
    }

    // ─── Verb builders ──────────────────────────────────────────────────────────

    private async Task<XElement> IdentifyAsync(string baseUrl, CancellationToken ct)
    {
        var earliest = await db.Set<SearchBib>().AsNoTracking().Select(b => (DateTimeOffset?)b.IndexedAt).MinAsync(ct) ?? DateTimeOffset.UtcNow;
        return new XElement(OaiNs + "Identify",
            new XElement(OaiNs + "repositoryName", _opts.RepositoryName),
            new XElement(OaiNs + "baseURL", baseUrl),
            new XElement(OaiNs + "protocolVersion", "2.0"),
            new XElement(OaiNs + "adminEmail", _opts.AdminEmail),
            new XElement(OaiNs + "earliestDatestamp", Ts(earliest)),
            // "transient": có báo xoá qua header status="deleted" (xem BuildHeader) nhưng không đảm bảo giữ mãi — search không có
            // bảng log xoá riêng, chỉ suy ra từ trạng thái hiện tại của bản sao (Deleted/Status, xem IsTombstone).
            new XElement(OaiNs + "deletedRecord", "transient"),
            new XElement(OaiNs + "granularity", "YYYY-MM-DDThh:mm:ssZ"));
    }

    private static XElement ListMetadataFormats() =>
        new(OaiNs + "ListMetadataFormats",
            new XElement(OaiNs + "metadataFormat",
                new XElement(OaiNs + "metadataPrefix", DcPrefix),
                new XElement(OaiNs + "schema", "http://www.openarchives.org/OAI/2.0/oai_dc.xsd"),
                new XElement(OaiNs + "metadataNamespace", OaiDcNs.NamespaceName)),
            new XElement(OaiNs + "metadataFormat",
                new XElement(OaiNs + "metadataPrefix", MarcPrefix),
                new XElement(OaiNs + "schema", "http://www.loc.gov/standards/marcxml/schema/MARC21slim.xsd"),
                new XElement(OaiNs + "metadataNamespace", MarcNs.NamespaceName)));

    private static XElement ListSets() =>
        new(OaiNs + "ListSets",
            new XElement(OaiNs + "set", new XElement(OaiNs + "setSpec", SetSpec), new XElement(OaiNs + "setName", "Sách in")));

    private async Task<XElement?> ListAsync(
        string verb, string? metadataPrefix, string? from, string? until, string? set, string? resumptionToken, List<(string Code, string Msg)> errors,
        CancellationToken ct)
    {
        var effPrefix = metadataPrefix;
        DateTimeOffset? effFrom = null, effUntil = null;
        var offset = 0;

        if (!string.IsNullOrEmpty(resumptionToken))
        {
            var state = DecodeToken(resumptionToken);
            if (state is null) { errors.Add(("badResumptionToken", "Token không hợp lệ hoặc đã hết hạn")); return null; }
            effPrefix = state.MetadataPrefix;
            offset = state.Offset;
            if (state.From is { } f && TryParse(f, out var f2)) effFrom = f2;
            if (state.Until is { } u && TryParse(u, out var u2)) effUntil = u2;
        }
        else
        {
            if (string.IsNullOrEmpty(effPrefix)) { errors.Add(("badArgument", "Thiếu metadataPrefix")); return null; }
            if (!ValidPrefixes.Contains(effPrefix)) { errors.Add(("cannotDisseminateFormat", $"Không hỗ trợ metadataPrefix '{effPrefix}'")); return null; }
            if (!string.IsNullOrEmpty(set) && set != SetSpec) { errors.Add(("noRecordsMatch", $"Không có set '{set}'")); return null; }
            if (!string.IsNullOrEmpty(from))
            {
                if (!TryParse(from, out var f)) { errors.Add(("badArgument", $"from không hợp lệ: '{from}'")); return null; }
                effFrom = f;
            }
            if (!string.IsNullOrEmpty(until))
            {
                if (!TryParse(until, out var u)) { errors.Add(("badArgument", $"until không hợp lệ: '{until}'")); return null; }
                effUntil = u;
            }
        }

        var query = db.Set<SearchBib>().AsNoTracking().AsQueryable();
        if (effFrom is { } ff) query = query.Where(b => b.IndexedAt >= ff);
        if (effUntil is { } uu) query = query.Where(b => b.IndexedAt <= uu);

        var total = await query.CountAsync(ct);
        if (total == 0) { errors.Add(("noRecordsMatch", "Không có bản ghi phù hợp với điều kiện lọc")); return null; }
        if (offset >= total) { errors.Add(("badResumptionToken", "Token đã hết hạn hoặc không hợp lệ")); return null; }

        var pageSize = Math.Max(1, _opts.PageSize);
        var page = await query.OrderBy(b => b.IndexedAt).ThenBy(b => b.Mfn).Skip(offset).Take(pageSize).ToListAsync(ct);

        var el = new XElement(OaiNs + verb);
        foreach (var bib in page) el.Add(verb == "ListRecords" ? BuildRecord(bib, effPrefix!) : BuildHeader(bib));

        var nextOffset = offset + page.Count;
        if (nextOffset < total)
            el.Add(new XElement(OaiNs + "resumptionToken", new XAttribute("completeListSize", total), new XAttribute("cursor", offset),
                EncodeToken(new ResumptionState(effPrefix, effFrom?.ToString("o", CultureInfo.InvariantCulture), effUntil?.ToString("o", CultureInfo.InvariantCulture), nextOffset))));
        else if (!string.IsNullOrEmpty(resumptionToken))
            el.Add(new XElement(OaiNs + "resumptionToken", new XAttribute("completeListSize", total), new XAttribute("cursor", offset)));

        return el;
    }

    // ─── Data access ────────────────────────────────────────────────────────────

    private async Task<SearchBib?> GetSingleAsync(string identifier, CancellationToken ct)
    {
        const string prefix = $"oai:{SetSpec}:";
        if (!identifier.StartsWith(prefix, StringComparison.Ordinal) || !long.TryParse(identifier[prefix.Length..], out var mfn)) return null;
        return await db.Set<SearchBib>().AsNoTracking().FirstOrDefaultAsync(b => b.Mfn == mfn, ct);
    }

    /// <summary>Không còn hiện trên OPAC (đã xoá khỏi catalog hoặc bị rút khỏi xuất bản) — báo "deleted", không kèm metadata.</summary>
    private static bool IsTombstone(SearchBib b) => b.Deleted || b.Status != SearchBib.OpacVisible;

    // ─── Record/header/metadata XML ─────────────────────────────────────────────

    private static string Identifier(SearchBib b) => $"oai:{SetSpec}:{b.Mfn.ToString(CultureInfo.InvariantCulture)}";

    private static XElement BuildHeader(SearchBib b)
    {
        var header = new XElement(OaiNs + "header",
            new XElement(OaiNs + "identifier", Identifier(b)),
            new XElement(OaiNs + "datestamp", Ts(b.IndexedAt)),
            new XElement(OaiNs + "setSpec", SetSpec));
        if (IsTombstone(b)) header.Add(new XAttribute("status", "deleted"));
        return header;
    }

    private static XElement BuildRecord(SearchBib b, string metadataPrefix)
    {
        // Bản ghi đã bị gỡ/thu hồi: chỉ có <header status="deleted">, KHÔNG có <metadata> — đúng chuẩn OAI-PMH.
        if (IsTombstone(b)) return new XElement(OaiNs + "record", BuildHeader(b));
        var metadata = metadataPrefix == MarcPrefix ? BuildMarcXml(b) : BuildDcXml(b);
        return new XElement(OaiNs + "record", BuildHeader(b), new XElement(OaiNs + "metadata", metadata));
    }

    private static XElement BuildDcXml(SearchBib b)
    {
        var dc = new XElement(OaiDcNs + "dc",
            new XAttribute(XNamespace.Xmlns + "oai_dc", OaiDcNs.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "dc", DcNs.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", XsiNs.NamespaceName),
            new XAttribute(XsiNs + "schemaLocation", "http://www.openarchives.org/OAI/2.0/oai_dc/ http://www.openarchives.org/OAI/2.0/oai_dc.xsd"));

        if (!string.IsNullOrWhiteSpace(b.Title)) dc.Add(new XElement(DcNs + "title", b.Title));
        if (!string.IsNullOrWhiteSpace(b.Author)) dc.Add(new XElement(DcNs + "creator", b.Author));
        if (!string.IsNullOrWhiteSpace(b.OtherAuthors)) dc.Add(new XElement(DcNs + "contributor", b.OtherAuthors));
        if (!string.IsNullOrWhiteSpace(b.Publisher)) dc.Add(new XElement(DcNs + "publisher", b.Publisher));
        if (!string.IsNullOrWhiteSpace(b.PublishYear)) dc.Add(new XElement(DcNs + "date", b.PublishYear));
        if (!string.IsNullOrWhiteSpace(b.Language)) dc.Add(new XElement(DcNs + "language", b.Language));
        if (!string.IsNullOrWhiteSpace(b.Summary)) dc.Add(new XElement(DcNs + "description", b.Summary));
        if (!string.IsNullOrWhiteSpace(b.Keywords)) dc.Add(new XElement(DcNs + "subject", b.Keywords));
        if (!string.IsNullOrWhiteSpace(b.Isbns)) dc.Add(new XElement(DcNs + "identifier", b.Isbns));
        dc.Add(new XElement(DcNs + "identifier", Identifier(b)));
        dc.Add(new XElement(DcNs + "type", "Text"));
        return dc;
    }

    /// <summary>Khung MARC21XML tối giản từ các trường tóm tắt của chỉ mục tra cứu — xem ghi chú đầu lớp (không phải MARC gốc).</summary>
    private static XElement BuildMarcXml(SearchBib b)
    {
        var rec = new XElement(MarcNs + "record",
            new XAttribute(XNamespace.Xmlns + "xsi", XsiNs.NamespaceName),
            new XAttribute(XsiNs + "schemaLocation", "http://www.loc.gov/MARC21/slim http://www.loc.gov/standards/marcxml/schema/MARC21slim.xsd"),
            new XElement(MarcNs + "leader", "00000nam a2200000   4500"),
            new XElement(MarcNs + "controlfield", new XAttribute("tag", "001"), b.Mfn.ToString(CultureInfo.InvariantCulture)));

        if (!string.IsNullOrWhiteSpace(b.Title))
            rec.Add(new XElement(MarcNs + "datafield", new XAttribute("tag", "245"), new XAttribute("ind1", "1"), new XAttribute("ind2", "0"),
                new XElement(MarcNs + "subfield", new XAttribute("code", "a"), b.Title)));

        if (!string.IsNullOrWhiteSpace(b.Author))
            rec.Add(new XElement(MarcNs + "datafield", new XAttribute("tag", "100"), new XAttribute("ind1", "1"), new XAttribute("ind2", " "),
                new XElement(MarcNs + "subfield", new XAttribute("code", "a"), b.Author)));

        if (!string.IsNullOrWhiteSpace(b.Publisher) || !string.IsNullOrWhiteSpace(b.PublishYear))
        {
            var df = new XElement(MarcNs + "datafield", new XAttribute("tag", "264"), new XAttribute("ind1", " "), new XAttribute("ind2", "1"));
            if (!string.IsNullOrWhiteSpace(b.Publisher)) df.Add(new XElement(MarcNs + "subfield", new XAttribute("code", "b"), b.Publisher));
            if (!string.IsNullOrWhiteSpace(b.PublishYear)) df.Add(new XElement(MarcNs + "subfield", new XAttribute("code", "c"), b.PublishYear));
            rec.Add(df);
        }

        if (!string.IsNullOrWhiteSpace(b.Ddc))
            rec.Add(new XElement(MarcNs + "datafield", new XAttribute("tag", "082"), new XAttribute("ind1", "0"), new XAttribute("ind2", " "),
                new XElement(MarcNs + "subfield", new XAttribute("code", "a"), b.Ddc)));

        if (!string.IsNullOrWhiteSpace(b.Keywords))
            rec.Add(new XElement(MarcNs + "datafield", new XAttribute("tag", "650"), new XAttribute("ind1", " "), new XAttribute("ind2", "0"),
                new XElement(MarcNs + "subfield", new XAttribute("code", "a"), b.Keywords)));

        return rec;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private static string Ts(DateTimeOffset d) => d.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static bool TryParse(string s, out DateTimeOffset dt)
    {
        var ok = DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed);
        dt = parsed;
        return ok;
    }

    private static string EncodeToken(ResumptionState s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(s)));

    private static ResumptionState? DecodeToken(string token)
    {
        try
        {
            return JsonSerializer.Deserialize<ResumptionState>(Encoding.UTF8.GetString(Convert.FromBase64String(token)));
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record ResumptionState(string? MetadataPrefix, string? From, string? Until, int Offset);
}
