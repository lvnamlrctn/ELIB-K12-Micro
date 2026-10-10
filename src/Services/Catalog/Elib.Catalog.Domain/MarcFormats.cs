using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Elib.BuildingBlocks.Domain;

namespace Elib.Catalog.Domain;

/// <summary>Một biểu ghi đọc từ file. <see cref="Error"/> khác null: biểu ghi hỏng cấu trúc, không dùng được (các biểu ghi khác vẫn đọc tiếp).</summary>
public sealed record MarcFileRecord(string Leader, IReadOnlyList<MarcField> Fields, string? Error = null);

public enum MarcFileFormat
{
    Iso2709,
    MarcXml,

    /// <summary>Text 3 dòng/trường (nhãn / chỉ thị / giá trị "$a…$b…") — file "rc_*.txt" hệ cũ xuất, một biểu ghi mỗi file.</summary>
    Text,
}

/// <summary>
/// Đọc/ghi file MARC (monolith: Iso2709Reader + MarcConvertController + marc-text-import.util.ts phía client).
/// Khác monolith: ISO2709 không ghi Leader/09 = 'a' vẫn nhận UTF-8 nếu dữ liệu là UTF-8 hợp lệ (nhiều phần mềm trong nước
/// ghi UTF-8 nhưng để trống vị trí này — đọc Latin-1 sẽ vỡ dấu tiếng Việt); thư mục trường sai độ dài thì tách theo ký tự
/// kết thúc trường; MARCXML cấm DTD.
/// </summary>
public static partial class MarcFormats
{
    public const string DefaultLeader = "00000nam a2200000 a 4500";

    private const byte RecordTerminator = 0x1D;
    private const byte FieldTerminator = 0x1E;
    private const byte SubfieldDelimiter = 0x1F;
    private const string MarcXmlNamespace = "http://www.loc.gov/MARC21/slim";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static MarcFileFormat Detect(byte[] data, string? fileName)
    {
        ArgumentNullException.ThrowIfNull(data);
        var start = SkipJunk(data);
        if (start < data.Length && data[start] == '<') return MarcFileFormat.MarcXml;
        if (fileName?.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) == true) return MarcFileFormat.MarcXml;
        var leaderDigits = data.Length - start >= 24 && data.AsSpan(start, 5).IndexOfAnyExceptInRange((byte)'0', (byte)'9') < 0;
        return leaderDigits && data.AsSpan(start).Contains(FieldTerminator) ? MarcFileFormat.Iso2709 : MarcFileFormat.Text;
    }

    /// <summary>Đọc file theo định dạng nhận ra từ nội dung. File không đọc được (XML hỏng…) → <see cref="BusinessRuleException"/>.</summary>
    public static List<MarcFileRecord> Read(byte[] data, string? fileName) => Detect(data, fileName) switch
    {
        MarcFileFormat.MarcXml => ReadMarcXml(data),
        MarcFileFormat.Iso2709 => ReadIso2709(data),
        _ => ReadText(Encoding.UTF8.GetString(data)) is { } record ? [record] : [],
    };

    // ─── ISO2709 ───────────────────────────────────────────────────────────────

    public static List<MarcFileRecord> ReadIso2709(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var records = new List<MarcFileRecord>();
        var position = 0;
        while (position < data.Length)
        {
            // Bỏ byte rác trước bản ghi: BOM, CR/LF chen giữa các bản ghi (file .txt hệ cũ xuất).
            while (position < data.Length && !char.IsAsciiDigit((char)data[position])) position++;
            if (position >= data.Length) break;
            var end = Array.IndexOf(data, RecordTerminator, position);
            var length = (end < 0 ? data.Length : end + 1) - position;
            if (length >= 24) records.Add(ReadIso2709Record(data.AsSpan(position, length)));
            position += length;
        }
        return records;
    }

    private static MarcFileRecord ReadIso2709Record(ReadOnlySpan<byte> data)
    {
        var leader = Encoding.ASCII.GetString(data[..24]);
        if (!int.TryParse(leader.AsSpan(12, 5), NumberStyles.None, CultureInfo.InvariantCulture, out var baseAddress)
            || baseAddress < 25 || baseAddress > data.Length)
            return new MarcFileRecord(leader, [], "Leader không hợp lệ (địa chỉ dữ liệu sai).");

        var encoding = leader[9] == 'a' || IsUtf8(data) ? Encoding.UTF8 : Encoding.Latin1;
        var directory = data[24..(baseAddress - 1)];
        var body = data[baseAddress..];
        var entries = directory.Length / 12;

        // Ưu tiên vị trí/độ dài trong thư mục; một mục sai (không kết thúc bằng 0x1E) → tách tuần tự theo 0x1E.
        var raw = new List<(string Tag, byte[] Value)>(entries);
        var sequential = false;
        for (var i = 0; i < entries; i++)
        {
            var entry = Encoding.ASCII.GetString(directory.Slice(i * 12, 12));
            if (!int.TryParse(entry.AsSpan(3, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var length)
                || !int.TryParse(entry.AsSpan(7, 5), NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
                || length < 1 || offset + length > body.Length || body[offset + length - 1] != FieldTerminator)
            {
                sequential = true;
                break;
            }
            raw.Add((entry[..3], body.Slice(offset, length - 1).ToArray()));
        }
        if (sequential)
        {
            raw.Clear();
            var rest = body;
            for (var i = 0; i < entries && rest.Length > 0; i++)
            {
                var stop = rest.IndexOfAny(FieldTerminator, RecordTerminator);
                var value = stop < 0 ? rest : rest[..stop];
                raw.Add((Encoding.ASCII.GetString(directory.Slice(i * 12, 3)), value.ToArray()));
                rest = stop < 0 || rest[stop] == RecordTerminator ? [] : rest[(stop + 1)..];
            }
        }

        var fields = new List<MarcField>(raw.Count);
        foreach (var (tag, value) in raw)
        {
            if (MarcRecord.IsControlTag(tag))
            {
                fields.Add(new MarcField(tag, Value: encoding.GetString(value)));
                continue;
            }
            if (value.Length < 2) continue;
            var subfields = new List<MarcSubfield>();
            var parts = value.AsSpan(2);
            while (parts.Length > 0)
            {
                if (parts[0] != SubfieldDelimiter)
                {
                    var skip = parts.IndexOf(SubfieldDelimiter); // dữ liệu trước $ đầu tiên — không thuộc trường con nào, bỏ
                    if (skip < 0) break;
                    parts = parts[skip..];
                    continue;
                }
                var next = parts[1..].IndexOf(SubfieldDelimiter);
                var piece = next < 0 ? parts[1..] : parts.Slice(1, next);
                if (piece.Length > 0) subfields.Add(new MarcSubfield(((char)piece[0]).ToString(), encoding.GetString(piece[1..])));
                parts = next < 0 ? [] : parts[(next + 1)..];
            }
            fields.Add(new MarcField(tag, Indicator(value[0]), Indicator(value[1]), Subfields: subfields));
        }
        return new MarcFileRecord(leader, fields);
    }

    /// <summary>Ghi ISO2709 UTF-8 (Leader/09 = 'a'). Trường dài hơn 9999 byte, hoặc vượt 99999 byte/biểu ghi, bị bỏ (giới hạn của định dạng).</summary>
    public static byte[] WriteIso2709(IEnumerable<MarcFileRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        using var output = new MemoryStream();
        foreach (var record in records) output.Write(WriteIso2709Record(record));
        return output.ToArray();
    }

    private static byte[] WriteIso2709Record(MarcFileRecord record)
    {
        var directory = new StringBuilder();
        using var body = new MemoryStream();
        foreach (var field in record.Fields)
        {
            using var bytes = new MemoryStream();
            if (MarcRecord.IsControlTag(field.Tag))
            {
                bytes.Write(Encoding.UTF8.GetBytes(Clean(field.Value)));
            }
            else
            {
                bytes.WriteByte(IndicatorByte(field.Ind1));
                bytes.WriteByte(IndicatorByte(field.Ind2));
                foreach (var sub in field.Subfields ?? [])
                {
                    var code = Clean(sub.Code);
                    if (code.Length == 0) continue;
                    bytes.WriteByte(SubfieldDelimiter);
                    bytes.Write(Encoding.UTF8.GetBytes(code[..1]));
                    bytes.Write(Encoding.UTF8.GetBytes(Clean(sub.Value)));
                }
            }
            bytes.WriteByte(FieldTerminator);
            var length = (int)bytes.Length;
            if (length > 9999 || 24 + (directory.Length + 12) + 1 + body.Length + length + 1 > 99999) continue;
            directory.Append(CultureInfo.InvariantCulture, $"{field.Tag}{length:D4}{body.Length:D5}");
            bytes.Position = 0;
            bytes.CopyTo(body);
        }

        var baseAddress = 24 + directory.Length + 1;
        var recordLength = baseAddress + (int)body.Length + 1;
        var leader = (record.Leader is { Length: 24 } l ? l : DefaultLeader).ToCharArray();
        recordLength.ToString("D5", CultureInfo.InvariantCulture).CopyTo(0, leader, 0, 5);
        leader[9] = 'a';
        leader[10] = '2';
        leader[11] = '2';
        baseAddress.ToString("D5", CultureInfo.InvariantCulture).CopyTo(0, leader, 12, 5);
        "4500".CopyTo(0, leader, 20, 4);

        using var output = new MemoryStream(recordLength);
        output.Write(Encoding.ASCII.GetBytes(leader));
        output.Write(Encoding.ASCII.GetBytes(directory.ToString()));
        output.WriteByte(FieldTerminator);
        body.Position = 0;
        body.CopyTo(output);
        output.WriteByte(RecordTerminator);
        return output.ToArray();
    }

    // ─── MARCXML ───────────────────────────────────────────────────────────────

    /// <summary>MARCXML: một &lt;record&gt; hoặc &lt;collection&gt; nhiều &lt;record&gt;, có hay không namespace.</summary>
    public static List<MarcFileRecord> ReadMarcXml(byte[] data)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true };
        var records = new List<MarcFileRecord>();
        try
        {
            using var reader = XmlReader.Create(new MemoryStream(data), settings);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "record")
                    records.Add(ReadXmlRecord(reader.ReadSubtree()));
            }
        }
        catch (XmlException ex)
        {
            throw new BusinessRuleException("MARC_FILE_INVALID", $"File MARCXML không hợp lệ: dòng {ex.LineNumber}, {ex.Message}");
        }
        return records;
    }

    private static MarcFileRecord ReadXmlRecord(XmlReader reader)
    {
        using (reader)
        {
            var leader = DefaultLeader;
            var fields = new List<MarcField>();
            reader.Read();
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element) continue;
                switch (reader.LocalName)
                {
                    case "leader":
                        var value = reader.ReadElementContentAsString();
                        if (value.Length == 24) leader = value;
                        break;
                    case "controlfield":
                        var tag = reader.GetAttribute("tag") ?? "";
                        fields.Add(new MarcField(tag, Value: reader.ReadElementContentAsString()));
                        break;
                    case "datafield":
                        fields.Add(ReadXmlDataField(reader));
                        break;
                }
            }
            return new MarcFileRecord(leader, fields);
        }
    }

    private static MarcField ReadXmlDataField(XmlReader reader)
    {
        var tag = reader.GetAttribute("tag") ?? "";
        var ind1 = reader.GetAttribute("ind1");
        var ind2 = reader.GetAttribute("ind2");
        var subfields = new List<MarcSubfield>();
        using (var sub = reader.ReadSubtree())
        {
            sub.Read();
            while (sub.Read())
            {
                if (sub.NodeType == XmlNodeType.Element && sub.LocalName == "subfield")
                {
                    var code = sub.GetAttribute("code") ?? "";
                    subfields.Add(new MarcSubfield(code, sub.ReadElementContentAsString()));
                }
            }
        }
        return new MarcField(tag, Indicator(ind1), Indicator(ind2), Subfields: subfields);
    }

    /// <summary>Ghi MARCXML (lược đồ MARC21 slim của LoC), bỏ ký tự XML không cho phép.</summary>
    public static void WriteMarcXml(Stream output, IEnumerable<MarcFileRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        using var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true });
        writer.WriteStartDocument();
        writer.WriteStartElement("collection", MarcXmlNamespace);
        foreach (var record in records)
        {
            writer.WriteStartElement("record", MarcXmlNamespace);
            writer.WriteElementString("leader", MarcXmlNamespace, record.Leader is { Length: 24 } l ? l : DefaultLeader);
            foreach (var field in record.Fields)
            {
                if (MarcRecord.IsControlTag(field.Tag))
                {
                    writer.WriteStartElement("controlfield", MarcXmlNamespace);
                    writer.WriteAttributeString("tag", field.Tag);
                    writer.WriteString(XmlSafe(field.Value));
                    writer.WriteEndElement();
                    continue;
                }
                writer.WriteStartElement("datafield", MarcXmlNamespace);
                writer.WriteAttributeString("tag", field.Tag);
                writer.WriteAttributeString("ind1", Indicator(field.Ind1));
                writer.WriteAttributeString("ind2", Indicator(field.Ind2));
                foreach (var sub in field.Subfields ?? [])
                {
                    writer.WriteStartElement("subfield", MarcXmlNamespace);
                    writer.WriteAttributeString("code", XmlSafe(sub.Code));
                    writer.WriteString(XmlSafe(sub.Value));
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    // ─── Text 3 dòng của hệ cũ ───────────────────────────────────────────────────

    /// <summary>
    /// Text 3 dòng/trường (monolith: parseMarcText phía client). "Ldr" là Leader; trường dữ liệu mà dòng giá trị không bắt đầu
    /// bằng "$" (trường cục bộ 900/907/911… của hệ cũ) bị bỏ. Không có trường nào → null.
    /// </summary>
    public static MarcFileRecord? ReadText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.TrimStart('﻿').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var leader = DefaultLeader;
        var fields = new List<MarcField>();
        for (var i = 0; i + 2 < lines.Length; i += 3)
        {
            var tag = lines[i].Trim();
            var indicators = lines[i + 1];
            var value = lines[i + 2];
            if (tag.Length == 0) continue;
            if (tag.Equals("ldr", StringComparison.OrdinalIgnoreCase) || tag == "000")
            {
                if (value.Length == 24) leader = value;
                continue;
            }
            if (MarcRecord.IsControlTag(tag))
            {
                fields.Add(new MarcField(tag, Value: value));
                continue;
            }
            var trimmed = value.TrimStart();
            if (!trimmed.StartsWith('$')) continue;
            var subfields = SubfieldPattern().Matches(trimmed).Select(m => new MarcSubfield(m.Groups[1].Value, m.Groups[2].Value.Trim())).ToList();
            fields.Add(new MarcField(tag, Indicator(indicators.Length > 0 ? indicators[0] : ' '), Indicator(indicators.Length > 1 ? indicators[1] : ' '), Subfields: subfields));
        }
        return fields.Count > 0 ? new MarcFileRecord(leader, fields) : null;
    }

    // ─── Tiện ích ──────────────────────────────────────────────────────────────

    private static int SkipJunk(byte[] data)
    {
        var start = data.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? 3 : 0;
        while (start < data.Length && data[start] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') start++;
        return start;
    }

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

    /// <summary>Chỉ thị: chữ/số giữ nguyên, còn lại (trống, "#", "|", byte 0…) là khoảng trắng.</summary>
    private static string Indicator(char value) => char.IsAsciiLetterOrDigit(value) ? value.ToString() : " ";

    private static string Indicator(byte value) => Indicator((char)value);

    private static string Indicator(string? value) => Indicator(string.IsNullOrEmpty(value) ? ' ' : value[0]);

    private static byte IndicatorByte(string? value) => (byte)Indicator(value)[0];

    /// <summary>Bỏ ký tự điều khiển ISO2709 khỏi giá trị (nếu không, bản ghi xuất ra bị cắt sai chỗ).</summary>
    private static string Clean(string? value)
    {
        var text = value ?? "";
        return text.AsSpan().IndexOfAny('\u001D', '\u001E', '\u001F') < 0 ? text
            : new string(text.Where(c => c is not ('\u001D' or '\u001E' or '\u001F')).ToArray());
    }

    private static string XmlSafe(string? value)
    {
        var text = value ?? "";
        foreach (var ch in text)
        {
            if (!XmlConvert.IsXmlChar(ch) && !char.IsSurrogate(ch))
                return new string(text.Where(c => XmlConvert.IsXmlChar(c) || char.IsSurrogate(c)).ToArray());
        }
        return text;
    }

    [GeneratedRegex(@"\$(.)([^$]*)")]
    private static partial Regex SubfieldPattern();
}
