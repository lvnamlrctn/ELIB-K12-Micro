using System.Text;
using System.Xml.Linq;

namespace ELIBAPI.Core.Common;

/// <summary>
/// Đọc/ghi bản ghi MARC dạng nhị phân ISO2709 (.mrc). Tách ra dùng chung từ
/// MarcConvertController để tái sử dụng cho tính năng "Thêm sách từ file Marc"
/// và giải mã bản ghi lấy về từ Z39.50.
/// </summary>
public static class Iso2709Reader
{
    public class Record
    {
        public string       Leader { get; set; } = "00000cam a2200000   4500";
        public List<Field>  Fields { get; set; } = new();
    }

    public class Field
    {
        public string  Tag      { get; set; } = "";
        public char    Ind1     { get; set; } = ' ';
        public char    Ind2     { get; set; } = ' ';
        public string? Control  { get; set; }
        public List<(char Code, string? Value)> Subfields { get; set; } = new();
    }

    public static List<Record> Parse(Stream stream)
    {
        var records = new List<Record>();
        var buffer  = new List<byte>(8192);

        int b;
        while ((b = stream.ReadByte()) != -1)
        {
            // Leader luôn mở đầu bằng 5 chữ số (độ dài bản ghi) — bỏ qua byte rác trước bản ghi: BOM UTF-8 của file
            // ".txt" hệ cũ xuất ra (Marc_TIMESTAMP.txt, Đợt 20), hoặc CR/LF chen giữa các bản ghi. Trước đây BOM làm
            // lệch vị trí đọc leader → bản ghi đầu bị loại, file chỉ 1 bản ghi báo "không tìm thấy bản ghi hợp lệ".
            if (buffer.Count == 0 && (b < '0' || b > '9')) continue;
            buffer.Add((byte)b);
            if (b == 0x1D) // record terminator
            {
                if (buffer.Count >= 24)
                {
                    var rec = ParseSingleRecord(buffer.ToArray());
                    if (rec != null) records.Add(rec);
                }
                buffer.Clear();
            }
        }
        // Last record without trailing terminator
        if (buffer.Count >= 24)
        {
            var rec = ParseSingleRecord(buffer.ToArray());
            if (rec != null) records.Add(rec);
        }
        return records;
    }

    public static List<Record> Parse(byte[] data) => Parse(new MemoryStream(data));

    private static Record? ParseSingleRecord(byte[] data)
    {
        if (data.Length < 24) return null;

        var leader = Encoding.ASCII.GetString(data, 0, 24);
        // Leader pos 09: 'a' = Unicode (UTF-8), ' ' = MARC-8 (map to Latin-1)
        var enc = leader.Length > 9 && leader[9] == 'a'
            ? Encoding.UTF8
            : Encoding.GetEncoding("iso-8859-1");

        if (!int.TryParse(leader[12..17], out var baseAddr) || baseAddr < 25 || baseAddr > data.Length)
            return null;

        var record = new Record { Leader = leader };

        int dirLen = baseAddr - 24 - 1; // exclude the 0x1E terminator after directory
        if (dirLen <= 0) return record;

        var dirText = Encoding.ASCII.GetString(data, 24, Math.Min(dirLen, data.Length - 24));
        int entries = dirLen / 12;

        for (int i = 0; i < entries; i++)
        {
            if (i * 12 + 12 > dirText.Length) break;
            var entry = dirText.Substring(i * 12, 12);
            var tag   = entry[..3];
            if (!int.TryParse(entry[3..7],  out var fieldLen))    continue;
            if (!int.TryParse(entry[7..12], out var fieldOffset)) continue;

            int start = baseAddr + fieldOffset;
            if (start >= data.Length) continue;
            int end = Math.Min(start + fieldLen, data.Length);

            var raw = data[start..end];
            if (raw.Length > 0 && raw[^1] == 0x1E) raw = raw[..^1];
            if (raw.Length > 0 && raw[^1] == 0x1D) raw = raw[..^1];

            var field = new Field { Tag = tag };

            if (string.CompareOrdinal(tag, "010") < 0)
            {
                // Control field: no indicators or subfields
                field.Control = enc.GetString(raw);
            }
            else
            {
                if (raw.Length < 2) continue;
                field.Ind1 = raw[0] == 0x20 || raw[0] == 0x00 ? ' ' : (char)raw[0];
                field.Ind2 = raw[1] == 0x20 || raw[1] == 0x00 ? ' ' : (char)raw[1];

                int p = 2;
                while (p < raw.Length)
                {
                    if (raw[p] != 0x1F || p + 1 >= raw.Length) { p++; continue; }
                    char code     = (char)raw[p + 1];
                    int  valStart = p + 2;
                    p = valStart;
                    while (p < raw.Length && raw[p] != 0x1F) p++;
                    field.Subfields.Add((code, enc.GetString(raw, valStart, p - valStart)));
                }
            }

            record.Fields.Add(field);
        }

        return record;
    }

    public static byte[] Write(IEnumerable<Record> records)
    {
        using var ms = new MemoryStream();
        foreach (var rec in records)
            ms.Write(SerializeRecord(rec));
        return ms.ToArray();
    }

    private static byte[] SerializeRecord(Record rec)
    {
        var enc        = Encoding.UTF8;
        var fieldParts = new List<byte[]>();
        var dirParts   = new List<string>();
        int offset     = 0;

        foreach (var f in rec.Fields)
        {
            byte[] body;
            if (f.Control != null)
            {
                body = enc.GetBytes(f.Control);
            }
            else
            {
                using var fms = new MemoryStream();
                fms.WriteByte((byte)(f.Ind1 == '\0' ? ' ' : f.Ind1));
                fms.WriteByte((byte)(f.Ind2 == '\0' ? ' ' : f.Ind2));
                foreach (var (code, value) in f.Subfields)
                {
                    fms.WriteByte(0x1F);
                    fms.WriteByte((byte)code);
                    fms.Write(enc.GetBytes(value ?? ""));
                }
                body = fms.ToArray();
            }

            var full = new byte[body.Length + 1];
            body.CopyTo(full, 0);
            full[^1] = 0x1E; // field terminator

            dirParts.Add($"{f.Tag}{full.Length:D4}{offset:D5}");
            fieldParts.Add(full);
            offset += full.Length;
        }

        var dirBytes = Encoding.ASCII.GetBytes(string.Concat(dirParts));
        int baseAddr = 24 + dirBytes.Length + 1;             // +1 for 0x1E after directory
        int recLen   = baseAddr + fieldParts.Sum(b => b.Length) + 1; // +1 for 0x1D

        // Patch leader lengths
        var leaderChars = (rec.Leader.Length >= 24 ? rec.Leader : "00000cam a2200000   4500").ToCharArray();
        var lenStr  = recLen.ToString("D5");
        var baseStr = baseAddr.ToString("D5");
        for (int i = 0; i < 5; i++) leaderChars[i]      = lenStr[i];
        for (int i = 0; i < 5; i++) leaderChars[12 + i] = baseStr[i];
        leaderChars[9] = 'a'; // mark as UTF-8

        using var ms = new MemoryStream(recLen);
        ms.Write(Encoding.ASCII.GetBytes(new string(leaderChars)));
        ms.Write(dirBytes);
        ms.WriteByte(0x1E);
        foreach (var fb in fieldParts) ms.Write(fb);
        ms.WriteByte(0x1D);
        return ms.ToArray();
    }

    // ─── MARCXML (MARC21 slim schema) parser ─────────────────────────────────

    /// <summary>Đọc 1 phần tử &lt;record&gt; MARCXML (bất kể namespace) thành danh sách Field.</summary>
    public static List<Field> ParseMarcXmlRecord(XElement recordEl)
    {
        var fields = new List<Field>();
        foreach (var el in recordEl.Elements())
        {
            var local = el.Name.LocalName;
            if (local == "controlfield")
            {
                fields.Add(new Field { Tag = el.Attribute("tag")?.Value ?? "", Control = el.Value });
            }
            else if (local == "datafield")
            {
                var f = new Field
                {
                    Tag  = el.Attribute("tag")?.Value ?? "",
                    Ind1 = (el.Attribute("ind1")?.Value ?? " ").FirstOrDefault(),
                    Ind2 = (el.Attribute("ind2")?.Value ?? " ").FirstOrDefault(),
                };
                foreach (var sf in el.Elements().Where(x => x.Name.LocalName == "subfield"))
                {
                    var code = sf.Attribute("code")?.Value;
                    if (!string.IsNullOrEmpty(code)) f.Subfields.Add((code[0], sf.Value));
                }
                fields.Add(f);
            }
        }
        return fields;
    }

    private static readonly XNamespace MarcXmlNs = "http://www.loc.gov/MARC21/slim";

    /// <summary>Đợt 22.3 — chiều ngược của ParseMarcXmlRecord (record → &lt;record&gt; MARCXML), dùng cho
    /// OAI-PMH (metadataPrefix=marc21) qua <see cref="MarcRecordBuilder"/> ở ELIBAPI.Infrastructure.</summary>
    public static XElement ToMarcXmlElement(Record rec)
    {
        var rEl = new XElement(MarcXmlNs + "record", new XElement(MarcXmlNs + "leader", rec.Leader));

        foreach (var f in rec.Fields)
        {
            if (f.Control != null)
            {
                rEl.Add(new XElement(MarcXmlNs + "controlfield", new XAttribute("tag", f.Tag), f.Control));
            }
            else
            {
                var df = new XElement(MarcXmlNs + "datafield",
                    new XAttribute("tag",  f.Tag),
                    new XAttribute("ind1", f.Ind1 == '\0' ? " " : f.Ind1.ToString()),
                    new XAttribute("ind2", f.Ind2 == '\0' ? " " : f.Ind2.ToString()));
                foreach (var (code, value) in f.Subfields)
                    df.Add(new XElement(MarcXmlNs + "subfield", new XAttribute("code", code), value ?? ""));
                rEl.Add(df);
            }
        }
        return rEl;
    }

    /// <summary>Đọc 1 file MARCXML (1 &lt;record&gt; đơn lẻ hoặc &lt;collection&gt; nhiều &lt;record&gt;) thành danh sách Record.</summary>
    public static List<Record> ParseMarcXml(Stream stream)
    {
        var doc = XDocument.Load(stream);
        var recordEls = doc.Descendants().Where(e => e.Name.LocalName == "record").ToList();
        if (recordEls.Count == 0 && doc.Root != null) recordEls.Add(doc.Root);

        var records = new List<Record>();
        foreach (var recordEl in recordEls)
        {
            var leaderEl = recordEl.Elements().FirstOrDefault(e => e.Name.LocalName == "leader");
            records.Add(new Record
            {
                Leader = leaderEl?.Value is { Length: >= 24 } l ? l : "00000cam a2200000   4500",
                Fields = ParseMarcXmlRecord(recordEl)
            });
        }
        return records;
    }
}
