using System.Text;
using System.Xml;
using System.Xml.Linq;
using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MarcRecord = ELIBAPI.Core.Common.Iso2709Reader.Record;
using MarcField  = ELIBAPI.Core.Common.Iso2709Reader.Field;

namespace ELIBAPI.API.Controllers.PrintBook;

[ApiController]
[Route("api/PrintBook/[controller]")]
[Authorize]
public class MarcConvertController : ControllerBase
{
    // ─── Endpoints ─────────────────────────────────────────────────────────────

    /// <summary>Dublin Core XML → ISO2709 binary (.mrc)</summary>
    [HttpPost("DublinCoreToMarc21")]
    [Consumes("multipart/form-data")]
    [Permission("CATALOG_BIBS", "add")]
    public IActionResult DublinCoreToMarc21(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("File không hợp lệ", 400));
        try
        {
            using var stream  = file.OpenReadStream();
            var records       = ParseDublinCore(stream);
            if (records.Count == 0)
                return BadRequest(ApiResponse<object>.Fail("Không tìm thấy bản ghi Dublin Core hợp lệ", 400));
            var bytes = WriteIso2709(records);
            return File(bytes, "application/octet-stream", "output.mrc");
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail($"Lỗi chuyển đổi: {ex.Message}", 400));
        }
    }

    /// <summary>ISO2709 binary (.mrc) → Dublin Core XML</summary>
    [HttpPost("Marc21ToDublinCore")]
    [Consumes("multipart/form-data")]
    [Permission("CATALOG_BIBS", "add")]
    public IActionResult Marc21ToDublinCore(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("File không hợp lệ", 400));
        try
        {
            using var stream = file.OpenReadStream();
            var records      = ParseIso2709(stream);
            if (records.Count == 0)
                return BadRequest(ApiResponse<object>.Fail("Không tìm thấy bản ghi MARC hợp lệ", 400));
            var doc = ToDublinCore(records);
            using var ms = new MemoryStream();
            doc.Save(new XmlTextWriter(ms, new UTF8Encoding(false)));
            return File(ms.ToArray(), "application/xml", "output-dc.xml");
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail($"Lỗi chuyển đổi: {ex.Message}", 400));
        }
    }

    /// <summary>ISO2709 binary (.mrc) → MARC21XML (LOC schema)</summary>
    [HttpPost("Marc21ToMarc21Xml")]
    [Consumes("multipart/form-data")]
    [Permission("CATALOG_BIBS", "add")]
    public IActionResult Marc21ToMarc21Xml(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("File không hợp lệ", 400));
        try
        {
            using var stream = file.OpenReadStream();
            var records      = ParseIso2709(stream);
            if (records.Count == 0)
                return BadRequest(ApiResponse<object>.Fail("Không tìm thấy bản ghi MARC hợp lệ", 400));
            var doc = ToMarcXml(records);
            using var ms = new MemoryStream();
            doc.Save(new XmlTextWriter(ms, new UTF8Encoding(false)));
            return File(ms.ToArray(), "application/xml", "output-marc21.xml");
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail($"Lỗi chuyển đổi: {ex.Message}", 400));
        }
    }

    /// <summary>ISO2709 binary (.mrc) → JSON cấu trúc MARC fields</summary>
    [HttpPost("Iso2709ToMarc21")]
    [Consumes("multipart/form-data")]
    [Permission("CATALOG_BIBS", "add")]
    public IActionResult Iso2709ToMarc21(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("File không hợp lệ", 400));
        try
        {
            using var stream = file.OpenReadStream();
            var records      = ParseIso2709(stream);
            var result = records.Select(rec => new
            {
                leader = rec.Leader,
                fields = rec.Fields.Select(f => f.Control != null
                    ? (object)new { tag = f.Tag, value = f.Control }
                    : new
                    {
                        tag       = f.Tag,
                        ind1      = f.Ind1.ToString(),
                        ind2      = f.Ind2.ToString(),
                        subfields = f.Subfields
                            .Select(s => new { code = s.Code.ToString(), value = s.Value ?? "" })
                            .ToList()
                    }).ToList()
            }).ToList();
            return Ok(ApiResponse<object>.Ok(result, $"Đã phân tích {records.Count} bản ghi MARC"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail($"Lỗi chuyển đổi: {ex.Message}", 400));
        }
    }

    /// <summary>ISO2709 (.mrc) hoặc MARCXML (.xml) → MarcField[] đúng shape frontend (dùng cho "Thêm sách từ file Marc").</summary>
    [HttpPost("MarcFileToFields")]
    [Consumes("multipart/form-data")]
    [Permission("CATALOG_BIBS", "add")]
    public IActionResult MarcFileToFields(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("File không hợp lệ", 400));
        try
        {
            using var stream = file.OpenReadStream();
            var records      = IsXmlFile(file, stream)
                ? Iso2709Reader.ParseMarcXml(stream)
                : ParseIso2709(stream);
            if (records.Count == 0)
                return BadRequest(ApiResponse<object>.Fail("Không tìm thấy bản ghi MARC hợp lệ", 400));

            var result = records.Select(rec => rec.Fields.Select(f => f.Control != null
                ? (object)new { tag = f.Tag, value = f.Control }
                : new
                {
                    tag       = f.Tag,
                    ind1      = f.Ind1.ToString(),
                    ind2      = f.Ind2.ToString(),
                    subFields = f.Subfields.Select(s => new { code = s.Code.ToString(), value = s.Value ?? "" }).ToList()
                }).ToList()).ToList();

            return Ok(ApiResponse<object>.Ok(result, $"Đã phân tích {records.Count} bản ghi MARC"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail($"Lỗi chuyển đổi: {ex.Message}", 400));
        }
    }

    // ─── ISO2709 Parser/Writer (dùng chung qua Iso2709Reader) ────────────────────

    /// <summary>Sniff nội dung file: true nếu là MARCXML (bắt đầu bằng "&lt;" sau khi bỏ BOM/whitespace), false nếu ISO2709 nhị phân.</summary>
    private static bool IsXmlFile(IFormFile file, Stream stream)
    {
        bool looksXml = file.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
        if (!looksXml)
        {
            var buf = new byte[64];
            int read = stream.Read(buf, 0, buf.Length);
            var text = Encoding.UTF8.GetString(buf, 0, read).TrimStart('﻿', ' ', '\t', '\r', '\n');
            looksXml = text.StartsWith("<");
        }
        stream.Position = 0;
        return looksXml;
    }

    private static List<MarcRecord> ParseIso2709(Stream stream) => Iso2709Reader.Parse(stream);

    private static byte[] WriteIso2709(IEnumerable<MarcRecord> records) => Iso2709Reader.Write(records);

    // ─── MARC21XML Converter ────────────────────────────────────────────────────

    private static readonly XNamespace MarcNs = "http://www.loc.gov/MARC21/slim";

    private static XDocument ToMarcXml(IEnumerable<MarcRecord> records)
    {
        var col = new XElement(MarcNs + "collection");

        foreach (var rec in records)
        {
            var rEl = new XElement(MarcNs + "record");
            rEl.Add(new XElement(MarcNs + "leader", rec.Leader));

            foreach (var f in rec.Fields)
            {
                if (f.Control != null)
                {
                    rEl.Add(new XElement(MarcNs + "controlfield",
                        new XAttribute("tag", f.Tag), f.Control));
                }
                else
                {
                    var df = new XElement(MarcNs + "datafield",
                        new XAttribute("tag",  f.Tag),
                        new XAttribute("ind1", f.Ind1 == '\0' ? " " : f.Ind1.ToString()),
                        new XAttribute("ind2", f.Ind2 == '\0' ? " " : f.Ind2.ToString()));
                    foreach (var (code, value) in f.Subfields)
                        df.Add(new XElement(MarcNs + "subfield",
                            new XAttribute("code", code), value ?? ""));
                    rEl.Add(df);
                }
            }
            col.Add(rEl);
        }
        return new XDocument(new XDeclaration("1.0", "UTF-8", null), col);
    }

    // ─── Dublin Core Converter ──────────────────────────────────────────────────

    private static readonly XNamespace DcNs    = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace OaiDcNs = "http://www.openarchives.org/OAI/2.0/oai_dc/";

    private static List<MarcRecord> ParseDublinCore(Stream stream)
    {
        var doc     = XDocument.Load(stream);
        var records = new List<MarcRecord>();

        // Find <oai_dc:dc> or <dc:dc> elements (local name is "dc")
        var dcEls = doc.Descendants()
            .Where(e => e.Name.LocalName == "dc")
            .ToList();

        // Fallback: treat root as single DC record
        if (dcEls.Count == 0 && doc.Root != null)
            dcEls.Add(doc.Root);

        foreach (var dcEl in dcEls)
        {
            var rec          = new MarcRecord { Leader = "00000cam a2200000   4500" };
            bool firstCreator = true;

            foreach (var el in dcEl.Elements())
            {
                var name  = el.Name.LocalName.ToLowerInvariant();
                var value = el.Value.Trim();
                if (string.IsNullOrEmpty(value)) continue;

                switch (name)
                {
                    case "title":
                        rec.Fields.Add(new MarcField
                        {
                            Tag = "245", Ind1 = '1', Ind2 = '0',
                            Subfields = new() { ('a', value) }
                        });
                        break;

                    case "creator":
                        if (firstCreator)
                        {
                            rec.Fields.Add(new MarcField
                            {
                                Tag = "100", Ind1 = '1', Ind2 = ' ',
                                Subfields = new() { ('a', value) }
                            });
                            firstCreator = false;
                        }
                        else
                        {
                            rec.Fields.Add(new MarcField
                            {
                                Tag = "700", Ind1 = '1', Ind2 = ' ',
                                Subfields = new() { ('a', value) }
                            });
                        }
                        break;

                    case "subject":
                        rec.Fields.Add(new MarcField
                        {
                            Tag = "650", Ind1 = ' ', Ind2 = '0',
                            Subfields = new() { ('a', value) }
                        });
                        break;

                    case "description":
                        rec.Fields.Add(new MarcField
                        {
                            Tag = "520", Ind1 = ' ', Ind2 = ' ',
                            Subfields = new() { ('a', value) }
                        });
                        break;

                    case "publisher":
                    {
                        var f264 = rec.Fields.FirstOrDefault(f => f.Tag == "264");
                        if (f264 == null)
                        {
                            f264 = new MarcField { Tag = "264", Ind1 = ' ', Ind2 = '1', Subfields = new() };
                            rec.Fields.Add(f264);
                        }
                        f264.Subfields.Add(('b', value));
                        break;
                    }

                    case "date":
                    {
                        var f264 = rec.Fields.FirstOrDefault(f => f.Tag == "264");
                        if (f264 == null)
                        {
                            f264 = new MarcField { Tag = "264", Ind1 = ' ', Ind2 = '1', Subfields = new() };
                            rec.Fields.Add(f264);
                        }
                        f264.Subfields.Add(('c', value));
                        break;
                    }

                    case "identifier":
                        if (value.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                            value.StartsWith("ftp",  StringComparison.OrdinalIgnoreCase))
                            rec.Fields.Add(new MarcField
                            {
                                Tag = "856", Ind1 = '4', Ind2 = '0',
                                Subfields = new() { ('u', value) }
                            });
                        else
                            rec.Fields.Add(new MarcField
                            {
                                Tag = "020", Ind1 = ' ', Ind2 = ' ',
                                Subfields = new() { ('a', value) }
                            });
                        break;

                    case "language":
                        rec.Fields.Add(new MarcField
                        {
                            Tag = "041", Ind1 = ' ', Ind2 = ' ',
                            Subfields = new() { ('a', value) }
                        });
                        break;

                    case "rights":
                        rec.Fields.Add(new MarcField
                        {
                            Tag = "540", Ind1 = ' ', Ind2 = ' ',
                            Subfields = new() { ('a', value) }
                        });
                        break;

                    case "type":
                        rec.Fields.Add(new MarcField
                        {
                            Tag = "655", Ind1 = ' ', Ind2 = '7',
                            Subfields = new() { ('a', value) }
                        });
                        break;

                    case "source":
                        rec.Fields.Add(new MarcField
                        {
                            Tag = "786", Ind1 = ' ', Ind2 = ' ',
                            Subfields = new() { ('t', value) }
                        });
                        break;

                    case "relation":
                        rec.Fields.Add(new MarcField
                        {
                            Tag = "787", Ind1 = ' ', Ind2 = ' ',
                            Subfields = new() { ('a', value) }
                        });
                        break;

                    case "coverage":
                        rec.Fields.Add(new MarcField
                        {
                            Tag = "522", Ind1 = ' ', Ind2 = ' ',
                            Subfields = new() { ('a', value) }
                        });
                        break;

                    case "format":
                        rec.Fields.Add(new MarcField
                        {
                            Tag = "338", Ind1 = ' ', Ind2 = ' ',
                            Subfields = new() { ('a', value) }
                        });
                        break;
                }
            }

            if (rec.Fields.Count > 0) records.Add(rec);
        }

        return records;
    }

    private static XDocument ToDublinCore(IEnumerable<MarcRecord> records)
    {
        var root = new XElement("records",
            new XAttribute(XNamespace.Xmlns + "oai_dc", OaiDcNs.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "dc",     DcNs.NamespaceName));

        foreach (var rec in records)
        {
            var dc = new XElement(OaiDcNs + "dc");

            // 245 $a $b → dc:title
            var f245 = rec.Fields.FirstOrDefault(f => f.Tag == "245");
            if (f245 != null)
            {
                var title = string.Join(" ", f245.Subfields
                    .Where(s => s.Code is 'a' or 'b')
                    .Select(s => s.Value?.TrimEnd('/', ' ') ?? "")).Trim();
                if (!string.IsNullOrWhiteSpace(title))
                    dc.Add(new XElement(DcNs + "title", title));
            }

            // 100/700 $a → dc:creator
            foreach (var f in rec.Fields.Where(f => f.Tag is "100" or "700"))
            {
                var v = f.Subfields.FirstOrDefault(s => s.Code == 'a').Value?.TrimEnd(',', ' ');
                if (!string.IsNullOrWhiteSpace(v))
                    dc.Add(new XElement(DcNs + "creator", v));
            }

            // 650/600/610/611/630/651 $a → dc:subject
            foreach (var f in rec.Fields.Where(f => f.Tag is "650" or "600" or "610" or "611" or "630" or "651"))
            {
                var v = f.Subfields.FirstOrDefault(s => s.Code == 'a').Value?.TrimEnd('.', ' ');
                if (!string.IsNullOrWhiteSpace(v))
                    dc.Add(new XElement(DcNs + "subject", v));
            }

            // 520 $a → dc:description
            foreach (var f in rec.Fields.Where(f => f.Tag == "520"))
            {
                var v = f.Subfields.FirstOrDefault(s => s.Code == 'a').Value;
                if (!string.IsNullOrWhiteSpace(v))
                    dc.Add(new XElement(DcNs + "description", v));
            }

            // 264/260 $b → dc:publisher, $c → dc:date
            var pubField = rec.Fields.FirstOrDefault(f => f.Tag is "264" or "260");
            if (pubField != null)
            {
                var pub = pubField.Subfields.FirstOrDefault(s => s.Code == 'b').Value?.TrimEnd(',', ' ');
                if (!string.IsNullOrWhiteSpace(pub))
                    dc.Add(new XElement(DcNs + "publisher", pub));

                var date = pubField.Subfields.FirstOrDefault(s => s.Code == 'c').Value?.TrimEnd('.', ' ');
                if (!string.IsNullOrWhiteSpace(date))
                    dc.Add(new XElement(DcNs + "date", date));
            }

            // 020 $a → dc:identifier (ISBN)
            foreach (var f in rec.Fields.Where(f => f.Tag == "020"))
            {
                var v = f.Subfields.FirstOrDefault(s => s.Code == 'a').Value;
                if (!string.IsNullOrWhiteSpace(v))
                    dc.Add(new XElement(DcNs + "identifier", v));
            }

            // 856 $u → dc:identifier (URL)
            foreach (var f in rec.Fields.Where(f => f.Tag == "856"))
            {
                var v = f.Subfields.FirstOrDefault(s => s.Code == 'u').Value;
                if (!string.IsNullOrWhiteSpace(v))
                    dc.Add(new XElement(DcNs + "identifier", v));
            }

            // 041 $a → dc:language
            foreach (var f in rec.Fields.Where(f => f.Tag == "041"))
            {
                var v = f.Subfields.FirstOrDefault(s => s.Code == 'a').Value;
                if (!string.IsNullOrWhiteSpace(v))
                    dc.Add(new XElement(DcNs + "language", v));
            }

            // 540 $a → dc:rights
            foreach (var f in rec.Fields.Where(f => f.Tag == "540"))
            {
                var v = f.Subfields.FirstOrDefault(s => s.Code == 'a').Value;
                if (!string.IsNullOrWhiteSpace(v))
                    dc.Add(new XElement(DcNs + "rights", v));
            }

            // 655 $a → dc:type
            foreach (var f in rec.Fields.Where(f => f.Tag == "655"))
            {
                var v = f.Subfields.FirstOrDefault(s => s.Code == 'a').Value;
                if (!string.IsNullOrWhiteSpace(v))
                    dc.Add(new XElement(DcNs + "type", v));
            }

            root.Add(dc);
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
    }

}
