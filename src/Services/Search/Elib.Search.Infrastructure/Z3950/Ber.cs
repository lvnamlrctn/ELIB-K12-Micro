using System.Text;

namespace Elib.Search.Infrastructure.Z3950;

/// <summary>
/// Mã hoá/giải mã BER tối thiểu cho Z39.50 (port từ monolith Z3950BerClient — đã chạy với máy chủ thật lx2.loc.gov). Module ASN.1
/// Z39-50-APDU-1995 dùng tagging EXPLICIT mặc định: field ghi "[n] IMPLICIT" thì tag [n] thay tag gốc (dùng giá trị thô *Raw), tag bọc
/// quanh CHOICE luôn EXPLICIT; "[n]" không kèm APPLICATION là CONTEXT-SPECIFIC; số tag ≥ 31 dùng dạng nhiều byte (X.690 §8.1.2.4).
/// </summary>
internal static class Ber
{
    public static byte[] IntRaw(int value)
    {
        if (value == 0) return [0x00];
        var bytes = new List<byte>();
        var v = value;
        while (v != 0 && v != -1)
        {
            bytes.Insert(0, (byte)(v & 0xFF));
            v >>= 8;
        }
        if (value > 0 && (bytes[0] & 0x80) != 0) bytes.Insert(0, 0x00);
        return [.. bytes];
    }

    public static byte[] BoolRaw(bool value) => [value ? (byte)0xFF : (byte)0x00];

    public static byte[] OctetRaw(string value) => Encoding.UTF8.GetBytes(value);

    public static byte[] BitStringRaw(params byte[] bits) => [0x00, .. bits];

    public static byte[] OidRaw(params int[] oid)
    {
        var bytes = new List<byte> { (byte)(40 * oid[0] + oid[1]) };
        for (var i = 2; i < oid.Length; i++)
        {
            var v = oid[i];
            var segment = new List<byte> { (byte)(v & 0x7F) };
            v >>= 7;
            while (v > 0)
            {
                segment.Insert(0, (byte)((v & 0x7F) | 0x80));
                v >>= 7;
            }
            bytes.AddRange(segment);
        }
        return [.. bytes];
    }

    /// <summary>OID có tag universal (field không gắn tag riêng, vd attributeSet của RPNQuery).</summary>
    public static byte[] Oid(params int[] oid) => Tlv(0x06, OidRaw(oid));

    public static byte[] Sequence(params byte[][] items) => Tlv(0x30, Concat(items));

    public static byte[] Context(int tag, byte[] content) => Tagged(0x80, constructed: false, tag, content);

    public static byte[] ContextConstructed(int tag, byte[] content) => Tagged(0x80, constructed: true, tag, content);

    /// <summary>PDU ngoài cùng: initRequest [20], searchRequest [22], presentRequest [24] — context, constructed (byte đầu 0xB4…).</summary>
    public static byte[] Pdu(int tag, byte[] body) => Tagged(0x80, constructed: true, tag, body);

    public static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(p => p)];

    private static byte[] Tlv(byte tag, byte[] content) => [tag, .. Length(content.Length), .. content];

    private static byte[] Tagged(byte classBits, bool constructed, int tag, byte[] content)
    {
        var flag = (byte)(classBits | (constructed ? 0x20 : 0x00));
        if (tag < 31) return [(byte)(flag | tag), .. Length(content.Length), .. content];
        var segments = new List<byte> { (byte)(tag & 0x7F) };
        for (var v = tag >> 7; v > 0; v >>= 7) segments.Insert(0, (byte)((v & 0x7F) | 0x80));
        return [(byte)(flag | 0x1F), .. segments, .. Length(content.Length), .. content];
    }

    private static byte[] Length(int length) => length switch
    {
        < 128 => [(byte)length],
        < 256 => [0x81, (byte)length],
        < 65536 => [0x82, (byte)(length >> 8), (byte)(length & 0xFF)],
        _ => [0x83, (byte)(length >> 16), (byte)((length >> 8) & 0xFF), (byte)(length & 0xFF)],
    };

    // ── Giải mã ─────────────────────────────────────────────────────────────

    /// <summary>Một phần tử BER: lớp (0 universal, 1 application, 2 context), constructed, số tag, nội dung.</summary>
    public readonly record struct Element(int Class, bool Constructed, int Tag, byte[] Content);

    /// <summary>
    /// Đọc phần tử tại <paramref name="pos"/>. Nhận cả độ dài bất định (0x80 … 00 00) — PresentResponse của YAZ/Metaproxy dùng dạng này cho các
    /// tầng bọc bản ghi MARC. Dữ liệu hỏng → false.
    /// </summary>
    public static bool TryRead(byte[] data, ref int pos, out Element element)
    {
        element = default;
        var start = pos;
        if (pos >= data.Length) return false;
        var first = data[pos++];
        var cls = (first >> 6) & 0x3;
        var constructed = (first & 0x20) != 0;
        var tag = first & 0x1F;
        if (tag == 0x1F)
        {
            tag = 0;
            while (pos < data.Length)
            {
                var b = data[pos++];
                tag = (tag << 7) | (b & 0x7F);
                if ((b & 0x80) == 0) break;
            }
        }
        if (pos >= data.Length)
        {
            pos = start;
            return false;
        }
        if (data[pos] == 0x80)
        {
            pos++;
            var contentStart = pos;
            while (pos < data.Length)
            {
                if (pos + 1 < data.Length && data[pos] == 0 && data[pos + 1] == 0)
                {
                    element = new Element(cls, constructed, tag, data[contentStart..pos]);
                    pos += 2;
                    return true;
                }
                if (!TryRead(data, ref pos, out _)) break;
            }
            element = new Element(cls, constructed, tag, data[contentStart..]);
            pos = data.Length;
            return true;
        }
        var lengthByte = data[pos++];
        long length = lengthByte;
        if ((lengthByte & 0x80) != 0)
        {
            var n = lengthByte & 0x7F;
            if (n is 0 or > 4)
            {
                pos = start;
                return false;
            }
            length = 0;
            for (var i = 0; i < n && pos < data.Length; i++) length = (length << 8) | data[pos++];
        }
        if (pos + length > data.Length)
        {
            pos = start;
            return false;
        }
        element = new Element(cls, constructed, tag, data[pos..(int)(pos + length)]);
        pos += (int)length;
        return true;
    }

    /// <summary>Các phần tử con trực tiếp của nội dung một phần tử constructed.</summary>
    public static List<Element> Children(byte[] content)
    {
        var list = new List<Element>();
        var pos = 0;
        while (pos < content.Length)
        {
            var before = pos;
            if (!TryRead(content, ref pos, out var e) || pos <= before) break;
            list.Add(e);
        }
        return list;
    }

    public static int ToInt(byte[] content)
    {
        var v = content.Length > 0 && (content[0] & 0x80) != 0 ? -1 : 0;
        foreach (var b in content.Take(4)) v = (v << 8) | b;
        return v;
    }

    /// <summary>Độ dài đầy đủ của phần tử đầu tiên (tag + độ dài + nội dung), null nếu chưa nhận đủ — biết khi nào đọc xong một PDU.</summary>
    public static int? Measure(ReadOnlySpan<byte> data, int start)
    {
        var pos = start;
        if (pos >= data.Length) return null;
        if ((data[pos++] & 0x1F) == 0x1F)
        {
            do
            {
                if (pos >= data.Length) return null;
            }
            while ((data[pos++] & 0x80) != 0);
        }
        if (pos >= data.Length) return null;
        var lengthByte = data[pos++];
        if (lengthByte == 0x80)
        {
            while (true)
            {
                if (pos + 1 >= data.Length) return null;
                if (data[pos] == 0 && data[pos + 1] == 0) return pos + 2 - start;
                if (Measure(data, pos) is not { } inner) return null;
                pos += inner;
            }
        }
        long length = lengthByte;
        if ((lengthByte & 0x80) != 0)
        {
            var n = lengthByte & 0x7F;
            if (n is 0 or > 4) return null;
            length = 0;
            for (var i = 0; i < n; i++)
            {
                if (pos >= data.Length) return null;
                length = (length << 8) | data[pos++];
            }
        }
        var end = pos + length;
        return end <= data.Length ? (int)(end - start) : null;
    }
}
