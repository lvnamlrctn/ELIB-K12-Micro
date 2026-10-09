using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ELIBAPI.Core.Common;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ELIBAPI.Infrastructure.Services;

public interface IOaiPmhService
{
    Task<XDocument> HandleRequestAsync(long tenantId, string baseUrl, string? verb, string? identifier,
        string? metadataPrefix, string? from, string? until, string? set, string? resumptionToken);
}

public class OaiPmhService : IOaiPmhService
{
    private readonly ELIBAPIDbContext _db;
    private readonly OaiPmhOptions    _opts;

    private static readonly XNamespace OaiNs   = "http://www.openarchives.org/OAI/2.0/";
    private static readonly XNamespace XsiNs   = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace OaiDcNs = "http://www.openarchives.org/OAI/2.0/oai_dc/";
    private static readonly XNamespace DcNs    = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace MarcNs  = "http://www.loc.gov/MARC21/slim";

    private const string DcPrefix   = "oai_dc";
    private const string MarcPrefix = "marc21";
    private static readonly string[] ValidPrefixes = { DcPrefix, MarcPrefix };

    public OaiPmhService(ELIBAPIDbContext db, IOptions<OaiPmhOptions> opts)
    {
        _db   = db;
        _opts = opts.Value;
    }

    public async Task<XDocument> HandleRequestAsync(long tenantId, string baseUrl, string? verb, string? identifier,
        string? metadataPrefix, string? from, string? until, string? set, string? resumptionToken)
    {
        var errors   = new List<(string Code, string Msg)>();
        var reqAttrs = new List<XAttribute>();
        XElement? body = null;

        switch (verb)
        {
            case "Identify":
                reqAttrs.Add(new XAttribute("verb", verb));
                body = await BuildIdentifyAsync(tenantId, baseUrl);
                break;

            case "ListMetadataFormats":
                reqAttrs.Add(new XAttribute("verb", verb));
                body = BuildListMetadataFormats();
                break;

            case "ListSets":
                reqAttrs.Add(new XAttribute("verb", verb));
                body = BuildListSets();
                break;

            case "GetRecord":
                reqAttrs.Add(new XAttribute("verb", verb));
                if (!string.IsNullOrEmpty(identifier))     reqAttrs.Add(new XAttribute("identifier", identifier));
                if (!string.IsNullOrEmpty(metadataPrefix)) reqAttrs.Add(new XAttribute("metadataPrefix", metadataPrefix));

                if (string.IsNullOrEmpty(identifier) || string.IsNullOrEmpty(metadataPrefix))
                    errors.Add(("badArgument", "identifier và metadataPrefix là bắt buộc"));
                else if (!ValidPrefixes.Contains(metadataPrefix))
                    errors.Add(("cannotDisseminateFormat", $"Không hỗ trợ metadataPrefix '{metadataPrefix}'"));
                else
                {
                    var item = await GetSingleItemAsync(tenantId, identifier);
                    if (item == null)
                        errors.Add(("idDoesNotExist", $"Không tìm thấy bản ghi '{identifier}'"));
                    else
                        body = new XElement(OaiNs + "GetRecord", await BuildRecordElementAsync(item, metadataPrefix, baseUrl));
                }
                break;

            case "ListIdentifiers":
            case "ListRecords":
                reqAttrs.Add(new XAttribute("verb", verb));
                if (!string.IsNullOrEmpty(metadataPrefix))    reqAttrs.Add(new XAttribute("metadataPrefix", metadataPrefix));
                if (!string.IsNullOrEmpty(from))              reqAttrs.Add(new XAttribute("from", from));
                if (!string.IsNullOrEmpty(until))              reqAttrs.Add(new XAttribute("until", until));
                if (!string.IsNullOrEmpty(set))                reqAttrs.Add(new XAttribute("set", set));
                if (!string.IsNullOrEmpty(resumptionToken))    reqAttrs.Add(new XAttribute("resumptionToken", resumptionToken));
                body = await BuildListAsync(verb, tenantId, baseUrl, metadataPrefix, from, until, set, resumptionToken, errors);
                break;

            case null:
            case "":
                errors.Add(("badVerb", "Thiếu tham số verb"));
                break;

            default:
                errors.Add(("badVerb", $"Verb không hợp lệ: '{verb}'"));
                break;
        }

        var root = new XElement(OaiNs + "OAI-PMH",
            new XAttribute(XNamespace.Xmlns + "xsi", XsiNs.NamespaceName),
            new XAttribute(XsiNs + "schemaLocation",
                "http://www.openarchives.org/OAI/2.0/ http://www.openarchives.org/OAI/2.0/OAI-PMH.xsd"),
            new XElement(OaiNs + "responseDate", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
            new XElement(OaiNs + "request", reqAttrs, baseUrl));

        if (errors.Count > 0)
            foreach (var (code, msg) in errors)
                root.Add(new XElement(OaiNs + "error", new XAttribute("code", code), msg));
        else if (body != null)
            root.Add(body);

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
    }

    // ─── Verb builders ──────────────────────────────────────────────────────────

    private async Task<XElement> BuildIdentifyAsync(long tenantId, string baseUrl)
    {
        var earliestBib = await _db.Bibs
            .Where(b => b.TenantId == tenantId && b.IsDelete != 2 && b.Status == "f")
            .Select(b => (DateTime?)b.CreatedRowDate).MinAsync();
        var earliestEbook = await _db.EbookItems
            .Where(e => e.TenantId == tenantId && e.IsDelete != 2 && e.Status == 2)
            .Select(e => (DateTime?)e.CreatedRowDate).MinAsync();

        var candidates = new[] { earliestBib, earliestEbook }.Where(d => d.HasValue).Select(d => d!.Value).ToList();
        var earliest   = candidates.Count > 0 ? candidates.Min() : DateTime.UtcNow;

        return new XElement(OaiNs + "Identify",
            new XElement(OaiNs + "repositoryName", _opts.RepositoryName),
            new XElement(OaiNs + "baseURL", baseUrl),
            new XElement(OaiNs + "protocolVersion", "2.0"),
            new XElement(OaiNs + "adminEmail", _opts.AdminEmail),
            new XElement(OaiNs + "earliestDatestamp", earliest.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")),
            // Đợt 22.3: "transient" — biểu ghi xoá mềm có thể xuất hiện lại (khôi phục), khớp đúng ngữ nghĩa
            // IsDelete=2 của ELIB hơn "no" (không bao giờ xoá) hay "persistent" (xoá thì header còn mãi mãi).
            new XElement(OaiNs + "deletedRecord", "transient"),
            new XElement(OaiNs + "granularity", "YYYY-MM-DDThh:mm:ssZ"));
    }

    private static XElement BuildListMetadataFormats() =>
        new(OaiNs + "ListMetadataFormats",
            new XElement(OaiNs + "metadataFormat",
                new XElement(OaiNs + "metadataPrefix", DcPrefix),
                new XElement(OaiNs + "schema", "http://www.openarchives.org/OAI/2.0/oai_dc.xsd"),
                new XElement(OaiNs + "metadataNamespace", OaiDcNs.NamespaceName)),
            new XElement(OaiNs + "metadataFormat",
                new XElement(OaiNs + "metadataPrefix", MarcPrefix),
                new XElement(OaiNs + "schema", "http://www.loc.gov/standards/marcxml/schema/MARC21slim.xsd"),
                new XElement(OaiNs + "metadataNamespace", MarcNs.NamespaceName)));

    private static XElement BuildListSets() =>
        new(OaiNs + "ListSets",
            new XElement(OaiNs + "set",
                new XElement(OaiNs + "setSpec", "printbook"),
                new XElement(OaiNs + "setName", "Sách in")),
            new XElement(OaiNs + "set",
                new XElement(OaiNs + "setSpec", "ebook"),
                new XElement(OaiNs + "setName", "Ebook")));

    private async Task<XElement?> BuildListAsync(string verb, long tenantId, string baseUrl,
        string? metadataPrefix, string? from, string? until, string? set, string? resumptionToken,
        List<(string Code, string Msg)> errors)
    {
        string? effSet = set, effPrefix = metadataPrefix;
        DateTime? effFrom = null, effUntil = null;
        int offset = 0;

        if (!string.IsNullOrEmpty(resumptionToken))
        {
            var state = DecodeResumptionToken(resumptionToken);
            if (state == null)
            {
                errors.Add(("badResumptionToken", "Token không hợp lệ hoặc đã hết hạn"));
                return null;
            }
            effSet = state.Set; effPrefix = state.MetadataPrefix; offset = state.Offset;
            if (state.From is not null && TryParseDate(state.From, out var f2)) effFrom = f2;
            if (state.Until is not null && TryParseDate(state.Until, out var u2)) effUntil = u2;
        }
        else
        {
            if (string.IsNullOrEmpty(effPrefix))
            {
                errors.Add(("badArgument", "Thiếu metadataPrefix"));
                return null;
            }
            if (!ValidPrefixes.Contains(effPrefix))
            {
                errors.Add(("cannotDisseminateFormat", $"Không hỗ trợ metadataPrefix '{effPrefix}'"));
                return null;
            }
            if (!string.IsNullOrEmpty(from))
            {
                if (!TryParseDate(from, out var f)) { errors.Add(("badArgument", $"from không hợp lệ: '{from}'")); return null; }
                effFrom = f;
            }
            if (!string.IsNullOrEmpty(until))
            {
                if (!TryParseDate(until, out var u)) { errors.Add(("badArgument", $"until không hợp lệ: '{until}'")); return null; }
                effUntil = u;
            }
        }

        var all = await FetchItemsAsync(tenantId, effSet, effFrom, effUntil);
        if (all.Count == 0)
        {
            errors.Add(("noRecordsMatch", "Không có bản ghi phù hợp với điều kiện lọc"));
            return null;
        }
        if (offset >= all.Count)
        {
            errors.Add(("badResumptionToken", "Token đã hết hạn hoặc không hợp lệ"));
            return null;
        }

        var pageSize = Math.Max(1, _opts.PageSize);
        var page     = all.Skip(offset).Take(pageSize).ToList();

        var el = new XElement(OaiNs + verb);
        foreach (var item in page)
            el.Add(verb == "ListRecords" ? await BuildRecordElementAsync(item, effPrefix!, baseUrl) : BuildHeaderElement(item));

        int nextOffset = offset + page.Count;
        if (nextOffset < all.Count)
        {
            var token = EncodeResumptionToken(new ResumptionState(effSet, effPrefix, effFrom?.ToString("o"), effUntil?.ToString("o"), nextOffset));
            el.Add(new XElement(OaiNs + "resumptionToken",
                new XAttribute("completeListSize", all.Count), new XAttribute("cursor", offset), token));
        }
        else if (!string.IsNullOrEmpty(resumptionToken))
        {
            el.Add(new XElement(OaiNs + "resumptionToken",
                new XAttribute("completeListSize", all.Count), new XAttribute("cursor", offset)));
        }

        return el;
    }

    // ─── Data access ────────────────────────────────────────────────────────────

    private async Task<List<OaiItem>> FetchItemsAsync(long tenantId, string? set, DateTime? from, DateTime? until)
    {
        var items = new List<OaiItem>();

        if (set is null or "printbook")
        {
            var rows = await (from b in _db.Bibs
                               join x in _db.BibXmls on b.Bibid equals x.BibId into xj
                               from x in xj.DefaultIfEmpty()
                               where b.TenantId == tenantId && b.IsDelete != 2 && b.Status == "f"
                               select new { b.Bibid, b.CreatedRowDate, b.UpdatedRowDate, x.Title, x.Author, x.Publisher, x.PublishDate, x.Keyword })
                              .ToListAsync();
            foreach (var r in rows)
            {
                var ds = r.UpdatedRowDate ?? r.CreatedRowDate ?? DateTime.UtcNow;
                if (from.HasValue && ds < from.Value) continue;
                if (until.HasValue && ds > until.Value) continue;
                items.Add(new OaiItem
                {
                    Identifier = $"oai:printbook:{r.Bibid}", SetSpec = "printbook", Datestamp = ds,
                    Title = r.Title, Author = r.Author, Publisher = r.Publisher, PublishDate = r.PublishDate, Subject = r.Keyword
                });
            }
        }

        if (set is null or "ebook")
        {
            var rows = await (from e in _db.EbookItems
                               join x in _db.EbookItemXmls on e.Id equals x.Id into xj
                               from x in xj.DefaultIfEmpty()
                               where e.TenantId == tenantId && e.IsDelete != 2 && e.Status == 2
                               select new { e.Id, e.CreatedRowDate, e.UpdatedRowDate, x.Title, x.Author, x.Publisher, x.PublishDate, x.Keyword })
                              .ToListAsync();
            foreach (var r in rows)
            {
                var ds = r.UpdatedRowDate ?? r.CreatedRowDate ?? DateTime.UtcNow;
                if (from.HasValue && ds < from.Value) continue;
                if (until.HasValue && ds > until.Value) continue;
                items.Add(new OaiItem
                {
                    Identifier = $"oai:ebook:{r.Id}", SetSpec = "ebook", Datestamp = ds,
                    Title = r.Title, Author = r.Author, Publisher = r.Publisher, PublishDate = r.PublishDate, Subject = r.Keyword
                });
            }
        }

        return items.OrderBy(i => i.Datestamp).ThenBy(i => i.Identifier).ToList();
    }

    private async Task<OaiItem?> GetSingleItemAsync(long tenantId, string identifier)
    {
        if (identifier.StartsWith("oai:printbook:") && long.TryParse(identifier["oai:printbook:".Length..], out var bibid))
        {
            var r = await (from b in _db.Bibs
                            join x in _db.BibXmls on b.Bibid equals x.BibId into xj
                            from x in xj.DefaultIfEmpty()
                            where b.Bibid == bibid && b.TenantId == tenantId && b.IsDelete != 2 && b.Status == "f"
                            select new { b.Bibid, b.CreatedRowDate, b.UpdatedRowDate, x.Title, x.Author, x.Publisher, x.PublishDate, x.Keyword })
                           .FirstOrDefaultAsync();
            if (r == null) return null;
            return new OaiItem
            {
                Identifier = identifier, SetSpec = "printbook", Datestamp = r.UpdatedRowDate ?? r.CreatedRowDate ?? DateTime.UtcNow,
                Title = r.Title, Author = r.Author, Publisher = r.Publisher, PublishDate = r.PublishDate, Subject = r.Keyword
            };
        }

        if (identifier.StartsWith("oai:ebook:") && long.TryParse(identifier["oai:ebook:".Length..], out var ebookId))
        {
            var r = await (from e in _db.EbookItems
                            join x in _db.EbookItemXmls on e.Id equals x.Id into xj
                            from x in xj.DefaultIfEmpty()
                            where e.Id == ebookId && e.TenantId == tenantId && e.IsDelete != 2 && e.Status == 2
                            select new { e.Id, e.CreatedRowDate, e.UpdatedRowDate, x.Title, x.Author, x.Publisher, x.PublishDate, x.Keyword })
                           .FirstOrDefaultAsync();
            if (r == null) return null;
            return new OaiItem
            {
                Identifier = identifier, SetSpec = "ebook", Datestamp = r.UpdatedRowDate ?? r.CreatedRowDate ?? DateTime.UtcNow,
                Title = r.Title, Author = r.Author, Publisher = r.Publisher, PublishDate = r.PublishDate, Subject = r.Keyword
            };
        }

        return null;
    }

    // ─── Record/header/metadata XML ─────────────────────────────────────────────

    private static XElement BuildHeaderElement(OaiItem item) =>
        new(OaiNs + "header",
            new XElement(OaiNs + "identifier", item.Identifier),
            new XElement(OaiNs + "datestamp", item.Datestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")),
            new XElement(OaiNs + "setSpec", item.SetSpec));

    private async Task<XElement> BuildRecordElementAsync(OaiItem item, string metadataPrefix, string baseUrl) =>
        new(OaiNs + "record",
            BuildHeaderElement(item),
            new XElement(OaiNs + "metadata", metadataPrefix == MarcPrefix ? await BuildMarcXmlAsync(item) : BuildDcXml(item)));

    private static XElement BuildDcXml(OaiItem item)
    {
        var dc = new XElement(OaiDcNs + "dc",
            new XAttribute(XNamespace.Xmlns + "oai_dc", OaiDcNs.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "dc", DcNs.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", XsiNs.NamespaceName),
            new XAttribute(XsiNs + "schemaLocation", "http://www.openarchives.org/OAI/2.0/oai_dc/ http://www.openarchives.org/OAI/2.0/oai_dc.xsd"));

        if (!string.IsNullOrWhiteSpace(item.Title))       dc.Add(new XElement(DcNs + "title", item.Title));
        if (!string.IsNullOrWhiteSpace(item.Author))      dc.Add(new XElement(DcNs + "creator", item.Author));
        if (!string.IsNullOrWhiteSpace(item.Publisher))   dc.Add(new XElement(DcNs + "publisher", item.Publisher));
        if (!string.IsNullOrWhiteSpace(item.PublishDate)) dc.Add(new XElement(DcNs + "date", item.PublishDate));
        if (!string.IsNullOrWhiteSpace(item.Subject))     dc.Add(new XElement(DcNs + "subject", item.Subject));
        dc.Add(new XElement(DcNs + "identifier", item.Identifier));
        dc.Add(new XElement(DcNs + "type", item.SetSpec == "printbook" ? "Text" : "Text.Electronic"));
        return dc;
    }

    /// <summary>Đợt 22.3 — sách in (printbook): dựng MARC đầy đủ từ dữ liệu biên mục thật (FixedFieldValue+
    /// BibData) qua MarcRecordBuilder; chỉ rơi về khung tối giản khi biểu ghi không có field nào (hiếm) hoặc
    /// đã bị xoá/chưa duyệt giữa lúc liệt kê và lúc dựng XML. Tài liệu số (ebook) không có BibData/MARC gốc
    /// nên vẫn dùng khung tối giản — giới hạn đã biết, không giả vờ có MARC đầy đủ cho tài liệu số.</summary>
    private async Task<XElement> BuildMarcXmlAsync(OaiItem item)
    {
        if (item.SetSpec == "printbook" && item.Identifier.StartsWith("oai:printbook:") &&
            long.TryParse(item.Identifier["oai:printbook:".Length..], out var bibid))
        {
            var record = await MarcRecordBuilder.BuildAsync(_db, bibid);
            if (record != null) return ELIBAPI.Core.Common.Iso2709Reader.ToMarcXmlElement(record);
        }
        return BuildMarcXmlSkeleton(item);
    }

    private static XElement BuildMarcXmlSkeleton(OaiItem item)
    {
        var rec = new XElement(MarcNs + "record",
            new XAttribute(XNamespace.Xmlns + "xsi", XsiNs.NamespaceName),
            new XAttribute(XsiNs + "schemaLocation", "http://www.loc.gov/MARC21/slim http://www.loc.gov/standards/marcxml/schema/MARC21slim.xsd"),
            new XElement(MarcNs + "leader", "00000nam a2200000   4500"),
            new XElement(MarcNs + "controlfield", new XAttribute("tag", "001"), item.Identifier));

        if (!string.IsNullOrWhiteSpace(item.Title))
            rec.Add(new XElement(MarcNs + "datafield", new XAttribute("tag", "245"), new XAttribute("ind1", "1"), new XAttribute("ind2", "0"),
                new XElement(MarcNs + "subfield", new XAttribute("code", "a"), item.Title)));

        if (!string.IsNullOrWhiteSpace(item.Author))
            rec.Add(new XElement(MarcNs + "datafield", new XAttribute("tag", "100"), new XAttribute("ind1", "1"), new XAttribute("ind2", " "),
                new XElement(MarcNs + "subfield", new XAttribute("code", "a"), item.Author)));

        if (!string.IsNullOrWhiteSpace(item.Publisher) || !string.IsNullOrWhiteSpace(item.PublishDate))
        {
            var df = new XElement(MarcNs + "datafield", new XAttribute("tag", "264"), new XAttribute("ind1", " "), new XAttribute("ind2", "1"));
            if (!string.IsNullOrWhiteSpace(item.Publisher))   df.Add(new XElement(MarcNs + "subfield", new XAttribute("code", "b"), item.Publisher));
            if (!string.IsNullOrWhiteSpace(item.PublishDate)) df.Add(new XElement(MarcNs + "subfield", new XAttribute("code", "c"), item.PublishDate));
            rec.Add(df);
        }

        if (!string.IsNullOrWhiteSpace(item.Subject))
            rec.Add(new XElement(MarcNs + "datafield", new XAttribute("tag", "650"), new XAttribute("ind1", " "), new XAttribute("ind2", "0"),
                new XElement(MarcNs + "subfield", new XAttribute("code", "a"), item.Subject)));

        return rec;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private static bool TryParseDate(string s, out DateTime dt) =>
        DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out dt);

    private static string EncodeResumptionToken(ResumptionState s) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(s)));

    private static ResumptionState? DecodeResumptionToken(string token)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            return JsonSerializer.Deserialize<ResumptionState>(json);
        }
        catch
        {
            return null;
        }
    }

    private record ResumptionState(string? Set, string? MetadataPrefix, string? From, string? Until, int Offset);

    private class OaiItem
    {
        public string    Identifier { get; set; } = "";
        public string    SetSpec    { get; set; } = "";
        public DateTime  Datestamp  { get; set; }
        public string?   Title, Author, Publisher, PublishDate, Subject;
    }
}
