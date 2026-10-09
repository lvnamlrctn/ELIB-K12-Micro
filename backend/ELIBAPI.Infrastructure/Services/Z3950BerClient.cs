using System.Runtime.InteropServices;
using System.Net.Sockets;
using System.Text;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>
/// Minimal Z39.50 client — implements Init + Search + Present over TCP using ASN.1 BER.
/// Covers the common BIB-1 attribute set for title/author/publisher/keyword/ISBN search.
///
/// Tag numbers below được đối chiếu trực tiếp với module ASN.1 Z39-50-APDU-1995 thật
/// (nguồn: Wireshark epan/dissectors/asn1/z3950/z3950.asn — bản dịch từ chuẩn LOC), KHÔNG suy
/// đoán — vì đây là cài đặt BER viết tay, không dùng thư viện ASN.1 tham chiếu nào. Module này
/// dùng tagging mặc định EXPLICIT (không có "IMPLICIT TAGS" sau DEFINITIONS); field nào ghi rõ
/// "IMPLICIT" thì tag đó thay thế tag gốc của kiểu dữ liệu, field/CHOICE nào không ghi IMPLICIT
/// thì tag đó bọc EXPLICIT bên ngoài encoding gốc. 1 số điểm quan trọng hay bị nhầm:
///  - 1 tag bọc quanh 1 CHOICE luôn là EXPLICIT (CHOICE không có "tag gốc" để IMPLICIT thay thế).
///  - Tag chỉ ghi "[n]" không có từ khoá APPLICATION/UNIVERSAL/PRIVATE → mặc định là CONTEXT-SPECIFIC
///    (kể cả AttributesPlusTerm "[102]", AttributeList "[44]", Term.general "[45]"... — không phải
///    APPLICATION như 1 số tài liệu tóm tắt không chính thức hay ghi nhầm).
///  - Số tag ≥ 31 bắt buộc dùng high-tag-number form (X.690 §8.1.2.4), kể cả khi số đó < 128.
/// </summary>
public class Z3950BerClient
{
    // BIB-1 Use Attributes
    private static readonly Dictionary<string, int> Bib1Use = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Title"]     = 4,
        ["Author"]    = 1003,
        ["Publisher"] = 1018,
        ["Keyword"]   = 1016,
        ["Subject"]   = 21,
        ["ISBN"]      = 7,
    };

    public async Task<(bool connected, int count, string? error)> SearchCountAsync(
        string host, int port, string database,
        string? user, string? password,
        List<(string field, string value)> terms, string op,
        int timeoutSeconds = 10)
    {
        try
        {
            using var tcp = new TcpClient();
            var ct = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            await tcp.ConnectAsync(host, port, ct.Token);
            using var stream = tcp.GetStream();

            // Init
            await SendPdu(stream, BuildInit(user, password));
            var initResp = await ReadPdu(stream, ct.Token);
            if (!ParseInitResponse(initResp))
                return (false, 0, "Init rejected by server");

            // Search
            var rpn = BuildRpnQuery(terms, op);
            await SendPdu(stream, BuildSearch(database, rpn));
            var searchResp = await ReadPdu(stream, ct.Token);
            var (ok, count, err) = ParseSearchResponse(searchResp);
            return (ok, count, err);
        }
        catch (Exception ex)
        {
            return (false, 0, ex.Message);
        }
    }

    public async Task<(bool connected, int total, List<Dictionary<string, string>> records, string? error)> SearchFetchAsync(
        string host, int port, string database,
        string? user, string? password,
        List<(string field, string value)> terms, string op,
        int startRecord, int maxRecords,
        int timeoutSeconds = 15)
    {
        try
        {
            using var tcp = new TcpClient();
            var ct = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            await tcp.ConnectAsync(host, port, ct.Token);
            using var stream = tcp.GetStream();

            await SendPdu(stream, BuildInit(user, password));
            var initResp = await ReadPdu(stream, ct.Token);
            if (!ParseInitResponse(initResp))
                return (false, 0, [], "Init rejected by server");

            var rpn = BuildRpnQuery(terms, op);
            await SendPdu(stream, BuildSearch(database, rpn));
            var searchResp = await ReadPdu(stream, ct.Token);
            var (ok, count, err) = ParseSearchResponse(searchResp);
            if (!ok) return (false, 0, [], err);

            if (count == 0) return (true, 0, [], null);

            await SendPdu(stream, BuildPresent(startRecord, Math.Min(maxRecords, count - startRecord + 1)));
            var presentResp = await ReadPdu(stream, ct.Token);
            var records = ExtractIso2709Records(presentResp).Select(SummarizeIso2709).ToList();
            if (records.Count == 0 && PresentDiagnostic(presentResp) is { } diag) return (true, count, records, diag);

            return (true, count, records, null);
        }
        catch (Exception ex)
        {
            return (false, 0, [], ex.Message);
        }
    }

    /// <summary>
    /// Giống <see cref="SearchFetchAsync"/> nhưng trả về bản ghi MARC (ISO2709) THÔ, đầy đủ —
    /// dùng cho tính năng "Thêm sách từ Z3950" (cần trọn bản ghi MARC gốc, không chỉ vài trường
    /// tóm tắt như SearchFetchAsync/ParsePresentResponse hiện có).
    /// </summary>
    public async Task<(bool connected, int total, List<byte[]> rawRecords, string? error)> SearchFetchRawAsync(
        string host, int port, string database,
        string? user, string? password,
        List<(string field, string value)> terms, string op,
        int startRecord, int maxRecords,
        int timeoutSeconds = 15)
    {
        try
        {
            using var tcp = new TcpClient();
            var ct = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            await tcp.ConnectAsync(host, port, ct.Token);
            using var stream = tcp.GetStream();

            await SendPdu(stream, BuildInit(user, password));
            var initResp = await ReadPdu(stream, ct.Token);
            if (!ParseInitResponse(initResp))
                return (false, 0, [], "Init rejected by server");

            var rpn = BuildRpnQuery(terms, op);
            await SendPdu(stream, BuildSearch(database, rpn));
            var searchResp = await ReadPdu(stream, ct.Token);
            var (ok, count, err) = ParseSearchResponse(searchResp);
            if (!ok) return (false, 0, [], err);
            if (count == 0) return (true, 0, [], null);

            await SendPdu(stream, BuildPresent(startRecord, Math.Min(maxRecords, count - startRecord + 1)));
            var presentResp = await ReadPdu(stream, ct.Token);
            var rawRecords  = ExtractIso2709Records(presentResp);
            if (rawRecords.Count == 0 && PresentDiagnostic(presentResp) is { } diag) return (true, count, rawRecords, diag);

            return (true, count, rawRecords, null);
        }
        catch (Exception ex)
        {
            return (false, 0, [], ex.Message);
        }
    }

    /// <summary>
    /// Trích xuất các bản ghi MARC (ISO2709) thô từ PresentResponse. Cấu trúc ASN.1 thật của
    /// PresentResponse lồng nhiều tầng SEQUENCE/CHOICE/EXTERNAL (NamePlusRecordList →
    /// NamePlusRecord → record EXTERNAL → encoding octet-aligned OCTET STRING chứa đúng bytes
    /// ISO2709) — thay vì bóc tách chính xác từng tầng theo tag số (rủi ro cao khi không có máy
    /// chủ Z3950 thật để đối chiếu), dùng chiến lược thực dụng: quét đệ quy mọi đoạn octet
    /// nguyên thuỷ trong PDU và nhận diện đoạn nào có leader ISO2709 hợp lệ (5 ký tự số đầu = độ
    /// dài bản ghi khai báo, khớp với độ dài đoạn dữ liệu). Cách này bền hơn với sai lệch tag cụ
    /// thể trong tầng EXTERNAL (không quan trọng đến kết quả cuối) hơn là parse chính xác từng tag.
    /// </summary>
    private static List<byte[]> ExtractIso2709Records(byte[] data)
    {
        var results = new List<byte[]>();
        CollectIso2709Candidates(data, results);
        return results;
    }

    private static void CollectIso2709Candidates(byte[] data, List<byte[]> results)
    {
        var pos = 0;
        while (pos < data.Length)
        {
            var before = pos;
            if (!TryReadTlv(data, ref pos, out var constructed, out var content)) break;

            if (LooksLikeIso2709(content))
                results.Add(content);
            else if (constructed)
                CollectIso2709Candidates(content, results);

            if (pos <= before) break; // an toàn, tránh vòng lặp vô hạn nếu dữ liệu hỏng
        }
    }

    /// <summary>
    /// Đọc 1 TLV tại vị trí hiện tại, hỗ trợ cả định dạng độ dài bất định (indefinite length,
    /// byte length = 0x80, kết thúc bằng cặp byte 00 00) — PresentResponse thật từ server LOC
    /// (Metaproxy/YAZ) dùng dạng này cho các tầng constructed lồng bản ghi MARC; hàm
    /// <see cref="ReadBerLength"/> cũ đọc byte 0x80 nhầm thành "độ dài 0", khiến việc quét tìm
    /// bản ghi ISO2709 không bao giờ đi sâu được vào đúng tầng chứa dữ liệu thật.
    /// </summary>
    private static bool TryReadTlv(byte[] data, ref int pos, out bool constructed, out byte[] content)
    {
        constructed = false;
        content = [];
        if (pos >= data.Length) return false;
        var startPos = pos;
        var (_, cons, _) = ReadTag(data, ref pos);
        constructed = cons;
        if (pos >= data.Length) { pos = startPos; return false; }

        if (data[pos] == 0x80) // indefinite length — chỉ hợp lệ với constructed
        {
            pos++;
            var contentStart = pos;
            while (true)
            {
                if (pos + 1 < data.Length && data[pos] == 0x00 && data[pos + 1] == 0x00)
                {
                    content = data[contentStart..pos];
                    pos += 2;
                    return true;
                }
                if (pos >= data.Length) { content = data[contentStart..]; return true; } // dữ liệu bị cắt cụt — lấy hết phần còn lại
                if (!TryReadTlv(data, ref pos, out _, out _)) { pos = data.Length; content = data[contentStart..]; return true; }
            }
        }

        var len = ReadBerLength(data, ref pos);
        if (len < 0 || pos + len > data.Length) { pos = startPos; return false; }
        content = data[pos..(pos + len)];
        pos += len;
        return true;
    }

    /// <summary>
    /// PresentResponse không kèm bản ghi mà máy chủ báo lỗi (presentStatus [27] ≠ success, hoặc
    /// nonSurrogateDiagnostic [130] mang mã Bib-1) → câu báo lỗi cho người dùng thay vì âm thầm
    /// trả "0 bản ghi". Ví dụ LOC trả mã 13 (Present request out-of-range) khi tạm chặn truy vấn dồn dập.
    /// </summary>
    public static string? PresentDiagnostic(byte[] pdu)
    {
        var pos = 0;
        if (!TryReadTlv(pdu, ref pos, out _, out var body)) return null;
        int? status = null, condition = null;
        pos = 0;
        while (pos < body.Length)
        {
            var start = pos;
            var (_, _, tag) = ReadTag(body, ref pos);
            pos = start;
            if (!TryReadTlv(body, ref pos, out _, out var content) || pos <= start) break;
            if (tag == 27 && content.Length > 0) status = ReadBerIntValue(content, 0, content.Length);
            if (tag == 130) condition = FindFirstInteger(content);
        }
        if (condition is null && (status is null or 0)) return null;
        return condition is { } c
            ? $"Máy chủ Z39.50 từ chối trả bản ghi (mã chẩn đoán Bib-1 {c}{(c == 13 ? ": yêu cầu ngoài phạm vi / tạm bị giới hạn" : "")}). Vui lòng thử lại sau."
            : $"Máy chủ Z39.50 không trả được bản ghi (presentStatus {status}). Vui lòng thử lại sau.";
    }

    private static int? FindFirstInteger(byte[] data)
    {
        var pos = 0;
        while (pos < data.Length)
        {
            var start = pos;
            var (cls, _, tag) = ReadTag(data, ref pos);
            pos = start;
            if (!TryReadTlv(data, ref pos, out var constructed, out var content) || pos <= start) return null;
            if (cls == 0 && tag == 2 && content.Length is > 0 and <= 4) return ReadBerIntValue(content, 0, content.Length);
            if (constructed && FindFirstInteger(content) is { } inner) return inner;
        }
        return null;
    }

    private static bool LooksLikeIso2709(byte[] data)
    {
        if (data.Length < 24) return false;
        for (int i = 0; i < 5; i++)
            if (data[i] < (byte)'0' || data[i] > (byte)'9') return false;
        if (!int.TryParse(Encoding.ASCII.GetString(data, 0, 5), out var declaredLen)) return false;
        // Cho phép lệch nhỏ vì một số server không khớp tuyệt đối phần đệm cuối bản ghi
        return declaredLen > 24 && Math.Abs(declaredLen - data.Length) <= 2;
    }

    // ── PDU builders ─────────────────────────────────────────────────────────
    // Tham chiếu module Z39-50-APDU-1995 (xem ghi chú đầu file cho quy tắc IMPLICIT/EXPLICIT).

    private static byte[] BuildInit(string? user, string? password)
    {
        // InitializeRequest ::= SEQUENCE{
        //   referenceId ReferenceId OPTIONAL,                          -- bỏ qua (optional)
        //   protocolVersion ProtocolVersion,                           -- ProtocolVersion ::= [3] IMPLICIT BIT STRING
        //   options Options,                                           -- Options ::= [4] IMPLICIT BIT STRING
        //   preferredMessageSize [5] IMPLICIT INTEGER,
        //   exceptionalRecordSize [6] IMPLICIT INTEGER,
        //   idAuthentication [7] CHOICE{ open VisibleString, idPass SEQUENCE{groupId[0],userId[1],password[2]}, ... } OPTIONAL,
        //   implementationName [111] IMPLICIT InternationalString OPTIONAL,
        //   ... }
        var body = new List<byte[]>
        {
            Context(3, BerBitStringRaw([0b11100000])),   // protocolVersion: version-1,2,3 (bit 0,1,2)
            Context(4, BerBitStringRaw([0b11000000])),   // options: search(0), present(1)
            Context(5, BerIntRaw(1048576)),               // preferredMessageSize
            Context(6, BerIntRaw(1048576)),               // exceptionalRecordSize
        };
        if (!string.IsNullOrEmpty(user))
        {
            // idAuthentication [7] EXPLICIT CHOICE{ idPass SEQUENCE{ userId [1], password [2] } }
            // "idPass" arm tự nó không có tag riêng (untagged CHOICE member) → dùng nguyên tag
            // SEQUENCE universal (0x30) do BerSeqOf phát ra, KHÔNG bọc thêm tag context nào khác.
            var idPass = BerSeqOf(
                Context(1, BerOctetRaw(user)),
                Context(2, BerOctetRaw(password ?? ""))
            );
            body.Add(ContextC(7, idPass));
        }
        // implementationName [111] IMPLICIT InternationalString OPTIONAL
        body.Add(Context(111, BerOctetRaw("ELIBAPI Z3950 Client")));

        return WrapApplication(20, Concat(body.ToArray()));
    }

    private static byte[] BuildSearch(string database, byte[] queryBytes)
    {
        // SearchRequest ::= SEQUENCE{
        //   smallSetUpperBound [13] IMPLICIT INTEGER,
        //   largeSetLowerBound [14] IMPLICIT INTEGER,
        //   mediumSetPresentNumber [15] IMPLICIT INTEGER,
        //   replaceIndicator [16] IMPLICIT BOOLEAN,
        //   resultSetName [17] IMPLICIT InternationalString,
        //   databaseNames [18] IMPLICIT SEQUENCE OF DatabaseName,      -- DatabaseName ::= [105] IMPLICIT InternationalString
        //   query [21] Query }                                        -- [21] EXPLICIT (Query là CHOICE)
        var body = new List<byte[]>
        {
            Context(13, BerIntRaw(0)),
            Context(14, BerIntRaw(1)),
            Context(15, BerIntRaw(0)),
            Context(16, BerBoolRaw(true)),
            Context(17, BerOctetRaw("default")),
            ContextC(18, Context(105, BerOctetRaw(database))),
            ContextC(21, queryBytes),
        };
        return WrapApplication(22, Concat(body.ToArray()));
    }

    private static byte[] BuildPresent(int startRecord, int numRecords)
    {
        // PresentRequest ::= SEQUENCE{
        //   resultSetId ResultSetId,                                  -- ResultSetId ::= [31] IMPLICIT InternationalString (untagged field → dùng tag của kiểu)
        //   resultSetStartPoint [30] IMPLICIT INTEGER,
        //   numberOfRecordsRequested [29] IMPLICIT INTEGER,
        //   preferredRecordSyntax [104] IMPLICIT OBJECT IDENTIFIER OPTIONAL }
        var body = new List<byte[]>
        {
            Context(31, BerOctetRaw("default")),
            Context(30, BerIntRaw(startRecord)),
            Context(29, BerIntRaw(numRecords)),
            // USMARC record syntax OID = 1.2.840.10003.5.10
            Context(104, BerOidRaw([1, 2, 840, 10003, 5, 10])),
        };
        return WrapApplication(24, Concat(body.ToArray()));
    }

    private static byte[] BuildRpnQuery(List<(string field, string value)> terms, string op)
    {
        // Query ::= CHOICE{ ..., type-1 [1] IMPLICIT RPNQuery, ... }
        // RPNQuery ::= SEQUENCE{ attributeSet AttributeSetId, rpn RPNStructure }  -- 2 field đều untagged
        if (terms.Count == 0) return [];

        var rpn = BuildSingleTermOperand(terms[0].field, terms[0].value);
        for (int i = 1; i < terms.Count; i++)
        {
            var right = BuildSingleTermOperand(terms[i].field, terms[i].value);
            var opCode = op.Equals("OR", StringComparison.OrdinalIgnoreCase) ? 1
                       : op.Equals("AND-NOT", StringComparison.OrdinalIgnoreCase) ? 2
                       : 0; // mặc định AND
            // RPNStructure.rpnRpnOp ::= [1] IMPLICIT SEQUENCE{ rpn1 RPNStructure, rpn2 RPNStructure, op Operator }
            rpn = ContextC(1, Concat(rpn, right, BuildOperator(opCode)));
        }

        var rpnQuery = Concat(
            BerOid([1, 2, 840, 10003, 3, 1]),  // attributeSet: BIB-1 — untagged OID, dùng nguyên tag universal
            rpn
        );
        return ContextC(1, rpnQuery); // Query.type-1 [1] IMPLICIT RPNQuery
    }

    /// <summary>RPNStructure.op ::= [0] Operand — [0] là EXPLICIT vì Operand là CHOICE.</summary>
    private static byte[] BuildSingleTermOperand(string field, string value) =>
        ContextC(0, BuildAttributesPlusTerm(field, value));

    /// <summary>
    /// Operator ::= [46] CHOICE{ and [0] IMPLICIT NULL, or [1] IMPLICIT NULL, and-not [2] IMPLICIT NULL, ... }
    /// Bản thân kiểu Operator được gán tag [46] ngay trên định nghĩa CHOICE (bọc EXPLICIT vì
    /// không có IMPLICIT), nên khi dùng làm field "op Operator" (untagged) trong RpnRpnOp, phải
    /// phát ra đủ 2 tầng: [46]{ [opCode]{ NULL rỗng } }.
    /// </summary>
    private static byte[] BuildOperator(int opCode) => ContextC(46, Context(opCode, []));

    private static byte[] BuildAttributesPlusTerm(string field, string value)
    {
        int useAttr = Bib1Use.TryGetValue(field, out var u) ? u : 1016; // 1016 = Any

        // AttributeElement ::= SEQUENCE{ attributeType [120] IMPLICIT INTEGER,
        //                                attributeValue CHOICE{ numeric [121] IMPLICIT INTEGER, ... } }
        // (attributeSet [1] bỏ qua vì optional — kế thừa BIB-1 đã khai báo ở RPNQuery)
        var attributeElement = BerSeqOf(
            Context(120, BerIntRaw(1)),        // attributeType = 1 (Use)
            Context(121, BerIntRaw(useAttr))   // attributeValue.numeric
        );
        // AttributeList ::= [44] IMPLICIT SEQUENCE OF AttributeElement
        var attributes = ContextC(44, attributeElement);
        // Term ::= CHOICE{ general [45] IMPLICIT OCTET STRING, ... }
        var term = Context(45, BerOctetRaw(value));
        // AttributesPlusTerm ::= [102] IMPLICIT SEQUENCE{ attributes AttributeList, term Term }
        return ContextC(102, Concat(attributes, term));
    }

    // ── PDU parsers ──────────────────────────────────────────────────────────

    private static bool ParseInitResponse(byte[] data)
    {
        // InitializeResponse.result ::= [12] IMPLICIT BOOLEAN
        if (data.Length < 5) return false;
        try
        {
            var pos = 0;
            SkipApplicationTag(data, ref pos);
            while (pos < data.Length - 1)
            {
                var startPos = pos;
                var (cls, constructed, tagNum) = ReadTag(data, ref pos);
                var len = ReadBerLength(data, ref pos);
                if (pos + len > data.Length) break;
                if (cls == 2 && !constructed && tagNum == 12)
                    return len > 0 && data[pos] != 0x00;
                pos += len;
                if (pos <= startPos) break;
            }
            // Không thấy field result tường minh — 1 số server bỏ qua field OPTIONAL này khi
            // chấp nhận; coi như thành công nếu nhận được PDU InitializeResponse hợp lệ.
            return true;
        }
        catch { return false; }
    }

    private static (bool ok, int count, string? error) ParseSearchResponse(byte[] data)
    {
        // SearchResponse ::= SEQUENCE{ resultCount [23] IMPLICIT INTEGER, ..., searchStatus [22] IMPLICIT BOOLEAN,
        //   records Records OPTIONAL }
        // Records ::= CHOICE{ responseRecords [28] ..., nonSurrogateDiagnostic [130] ..., multipleNonSurDiagnostics [205] ... }
        try
        {
            var pos = 0;
            SkipApplicationTag(data, ref pos);
            int? resultCount = null;
            bool? searchStatus = null;
            string? diag = null;
            while (pos < data.Length - 1)
            {
                var startPos = pos;
                var (cls, constructed, tagNum) = ReadTag(data, ref pos);
                var len = ReadBerLength(data, ref pos);
                if (pos + len > data.Length) break;
                if (cls == 2 && !constructed && tagNum == 23)
                    resultCount = ReadBerIntValue(data, pos, len);
                else if (cls == 2 && !constructed && tagNum == 22)
                    searchStatus = len > 0 && data[pos] != 0x00;
                else if (cls == 2 && (tagNum == 130 || tagNum == 205))
                    diag = ExtractStringsFromBer(data[pos..(pos + len)]).FirstOrDefault();
                pos += len;
                if (pos <= startPos) break;
            }
            if (resultCount.HasValue) return (true, resultCount.Value, diag);
            if (searchStatus == false) return (false, 0, diag ?? "Server từ chối câu truy vấn (searchStatus=false)");
            return (false, 0, diag ?? "Could not parse resultCount from SearchResponse");
        }
        catch (Exception ex)
        {
            return (false, 0, ex.Message);
        }
    }

    /// <summary>
    /// Rút gọn 1 bản ghi ISO2709 (MARC21 nhị phân — đúng khổ mẫu BuildPresent yêu cầu qua
    /// preferredRecordSyntax=USMARC) thành vài trường tóm tắt hiển thị ở bảng kết quả tìm kiếm.
    /// Trước đây dùng ParsePresentResponse quét tìm chuỗi XML trong PDU — luôn trả về rỗng vì
    /// server không hề trả XML (chỉ trả đúng khổ mẫu đã yêu cầu là ISO2709), khiến bảng kết quả
    /// luôn "0 kết quả" dù resultCount thực tế > 0 và Present đã nhận đủ bytes bản ghi thật.
    /// </summary>
    private static Dictionary<string, string> SummarizeIso2709(byte[] raw)
    {
        var rec = ELIBAPI.Core.Common.Iso2709Reader.Parse(raw).FirstOrDefault();
        var result = new Dictionary<string, string>();
        if (rec == null) return result;

        string? SubA(string tag) => rec.Fields.FirstOrDefault(f => f.Tag == tag)?.Subfields.FirstOrDefault(s => s.Code == 'a').Value;

        var title = SubA("245");
        if (!string.IsNullOrWhiteSpace(title)) result["Title"] = title!.TrimEnd('/', ' ', ':');
        var author = SubA("100") ?? SubA("110") ?? SubA("111");
        if (!string.IsNullOrWhiteSpace(author)) result["Author"] = author!.TrimEnd(',', ' ');
        var publisher = rec.Fields.FirstOrDefault(f => f.Tag is "260" or "264")?.Subfields.FirstOrDefault(s => s.Code == 'b').Value;
        if (!string.IsNullOrWhiteSpace(publisher)) result["Publisher"] = publisher!.TrimEnd(',', ' ');
        var date = rec.Fields.FirstOrDefault(f => f.Tag is "260" or "264")?.Subfields.FirstOrDefault(s => s.Code == 'c').Value;
        if (!string.IsNullOrWhiteSpace(date)) result["PublishDate"] = date!.TrimEnd('.', ' ');
        var keyword = SubA("650");
        if (!string.IsNullOrWhiteSpace(keyword)) result["Keyword"] = keyword!.TrimEnd('.', ' ');
        return result;
    }

    private static List<string> ExtractStringsFromBer(byte[] data)
    {
        var result = new List<string>();
        var pos = 0;
        while (pos < data.Length - 2)
        {
            var tagByte = data[pos];
            pos++;
            if (pos >= data.Length) break;
            var len = ReadBerLength(data, ref pos);
            if (len < 0 || pos + len > data.Length) break;
            var primitiveType = tagByte & 0x20;
            if (primitiveType == 0 && len > 5)  // primitive, possibly a string
            {
                try
                {
                    var str = Encoding.UTF8.GetString(data, pos, len);
                    if (str.Any(c => c >= 32 && c <= 127)) result.Add(str);
                }
                catch { }
            }
            if (primitiveType != 0)  // constructed — recurse
            {
                result.AddRange(ExtractStringsFromBer(data[pos..(pos + len)]));
            }
            pos += len;
        }
        return result;
    }

    // ── BER encoding helpers ─────────────────────────────────────────────────

    private static byte[] BerInt(int value)
    {
        // Encode integer as minimum bytes, big-endian, 2's complement
        if (value == 0) return [0x02, 0x01, 0x00];
        var bytes = new List<byte>();
        var v = value;
        while (v != 0 && v != -1)
        {
            bytes.Insert(0, (byte)(v & 0xFF));
            v >>= 8;
        }
        // Add sign byte if needed
        if (value > 0 && (bytes[0] & 0x80) != 0) bytes.Insert(0, 0x00);
        return [0x02, (byte)bytes.Count, .. bytes];
    }

    private static byte[] BerBool(bool v) => [0x01, 0x01, v ? (byte)0xFF : (byte)0x00];

    private static byte[] BerOctet(string s)
    {
        var b = Encoding.UTF8.GetBytes(s);
        return [0x04, .. BerLength(b.Length), .. b];
    }

    private static byte[] BerBitString(byte[] bits)
    {
        // Prepend unused-bits count byte (0)
        return [0x03, (byte)(bits.Length + 1), 0x00, .. bits];
    }

    private static byte[] BerOid(int[] oid)
    {
        // OID encoding: first byte = 40*oid[0] + oid[1], then base-128 remaining
        var bytes = new List<byte> { (byte)(40 * oid[0] + oid[1]) };
        for (int i = 2; i < oid.Length; i++)
        {
            var v = oid[i];
            var seg = new List<byte>();
            seg.Add((byte)(v & 0x7F));
            v >>= 7;
            while (v > 0) { seg.Insert(0, (byte)((v & 0x7F) | 0x80)); v >>= 7; }
            bytes.AddRange(seg);
        }
        return [0x06, .. BerLength(bytes.Count), .. bytes];
    }

    private static byte[] BerSeqOf(params byte[][] items)
    {
        var body = Concat(items);
        return [0x30, .. BerLength(body.Length), .. body];
    }

    // ── "Raw value" encoders (KHÔNG có tag/length riêng) ─────────────────────
    // Dùng cho mọi field "[n] IMPLICIT <kiểu>" — tag [n] (từ Context/ContextC) THAY THẾ tag
    // universal của kiểu gốc, nên nội dung được bọc phải là bytes-giá-trị THUẦN TUÝ, không được
    // mang theo tag+length universal của chính nó (nếu không sẽ bị lồng tag 2 lần — 1 lỗi có thật
    // đã phát hiện khi test với server thật lx2.loc.gov: mọi field IMPLICIT trong Init/Search/
    // Present đều bị lồng tag kép, khiến server đóng kết nối ngay từ bước Init).
    // Ngược lại, BerInt/BerBool/BerOctet/BerBitString/BerOid (bên trên) trả về TLV đầy đủ (kèm
    // tag universal) — chỉ dùng cho field KHÔNG có tag riêng (untagged), nơi tag universal gốc
    // vẫn được giữ nguyên trên dây (vd AttributeSetId trong RPNQuery, AttributeElement dạng SEQUENCE).

    private static byte[] BerIntRaw(int value)
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
        return bytes.ToArray();
    }

    private static byte[] BerBoolRaw(bool v) => [v ? (byte)0xFF : (byte)0x00];

    private static byte[] BerOctetRaw(string s) => Encoding.UTF8.GetBytes(s);

    private static byte[] BerBitStringRaw(byte[] bits) => [0x00, .. bits];

    private static byte[] BerOidRaw(int[] oid)
    {
        var bytes = new List<byte> { (byte)(40 * oid[0] + oid[1]) };
        for (int i = 2; i < oid.Length; i++)
        {
            var v = oid[i];
            var seg = new List<byte> { (byte)(v & 0x7F) };
            v >>= 7;
            while (v > 0) { seg.Insert(0, (byte)((v & 0x7F) | 0x80)); v >>= 7; }
            bytes.AddRange(seg);
        }
        return bytes.ToArray();
    }

    /// <summary>
    /// Mã hoá 1 tag CONTEXT-SPECIFIC, tự động chọn low-tag-number form (1 byte, số tag ≤30) hay
    /// high-tag-number form (X.690 §8.1.2.4: byte đầu = class|constructed|0x1F, các byte sau là số
    /// tag dạng base-128 với bit tiếp diễn) — đây là phần trước đây bị thiếu hoàn toàn, khiến mọi
    /// tag ≥31 (vd AttributesPlusTerm=102, AttributeList=44, Term.general=45, DatabaseName=105...)
    /// bị mã hoá sai.
    /// </summary>
    private static byte[] Context(int tagNumber, byte[] content) => TagBytes(0x80, false, tagNumber, content);

    private static byte[] ContextC(int tagNumber, byte[] content) => TagBytes(0x80, true, tagNumber, content);

    private static byte[] TagBytes(byte classBits, bool constructed, int tagNumber, byte[] content)
    {
        var flag = (byte)(classBits | (constructed ? 0x20 : 0x00));
        byte[] tagPart;
        if (tagNumber < 31)
        {
            tagPart = [(byte)(flag | tagNumber)];
        }
        else
        {
            var segs = new List<byte> { (byte)(tagNumber & 0x7F) };
            var v = tagNumber >> 7;
            while (v > 0) { segs.Insert(0, (byte)((v & 0x7F) | 0x80)); v >>= 7; }
            tagPart = [(byte)(flag | 0x1F), .. segs];
        }
        return [.. tagPart, .. BerLength(content.Length), .. content];
    }

    private static byte[] WrapApplication(int pduTag, byte[] body)
    {
        // PDU ::= CHOICE{ initRequest [20] IMPLICIT ..., initResponse [21] ..., searchRequest [22] ...,
        //   searchResponse [23] ..., presentRequest [24] ..., presentResponse [25] ..., ... }
        // Bracket số không kèm từ khoá APPLICATION → mặc định CONTEXT-SPECIFIC (đã xác nhận bằng
        // packet capture thật qua proxy TCP đối chiếu với yaz-client: byte đầu InitializeRequest
        // thật là 0xB4 = context|constructed|20, KHÔNG phải 0x74 = application|constructed|20 như
        // code cũ dùng — đây là nguyên nhân gốc khiến server đóng kết nối ngay từ bước Init).
        // Các tag PDU top-level (20-25) đều ≤30 nên low-form 1 byte là đủ.
        byte tagByte = (byte)(0xA0 | (pduTag & 0x1F));
        return [tagByte, .. BerLength(body.Length), .. body];
    }

    private static byte[] BerLength(int len)
    {
        if (len < 128) return [(byte)len];
        if (len < 256) return [0x81, (byte)len];
        if (len < 65536) return [0x82, (byte)(len >> 8), (byte)(len & 0xFF)];
        return [0x83, (byte)(len >> 16), (byte)((len >> 8) & 0xFF), (byte)(len & 0xFF)];
    }

    private static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(p => p)];

    // ── BER decoding helpers ─────────────────────────────────────────────────

    private static void SkipApplicationTag(byte[] data, ref int pos)
    {
        if (pos >= data.Length) return;
        ReadTag(data, ref pos);
        ReadBerLength(data, ref pos);  // skip length
    }

    /// <summary>Đọc 1 tag BER tại vị trí hiện tại (hỗ trợ cả high-tag-number form), trả về
    /// (class: 0=universal,1=application,2=context,3=private; constructed; tagNumber) và tiến pos.</summary>
    private static (int cls, bool constructed, int tagNumber) ReadTag(byte[] data, ref int pos)
    {
        var first = data[pos++];
        var cls = (first >> 6) & 0x3;
        var constructed = (first & 0x20) != 0;
        var tagNumber = first & 0x1F;
        if (tagNumber == 0x1F)
        {
            tagNumber = 0;
            while (pos < data.Length)
            {
                var b = data[pos++];
                tagNumber = (tagNumber << 7) | (b & 0x7F);
                if ((b & 0x80) == 0) break;
            }
        }
        return (cls, constructed, tagNumber);
    }

    private static int ReadBerLength(byte[] data, ref int pos)
    {
        if (pos >= data.Length) return 0;
        var first = data[pos++];
        if ((first & 0x80) == 0) return first;
        var numBytes = first & 0x7F;
        var len = 0;
        for (int i = 0; i < numBytes && pos < data.Length; i++)
            len = (len << 8) | data[pos++];
        return len;
    }

    private static int ReadBerIntValue(byte[] data, int pos, int len)
    {
        var v = 0;
        for (int i = 0; i < len; i++) v = (v << 8) | data[pos + i];
        return v;
    }

    /// <summary>Độ dài trọn vẹn của TLV bắt đầu tại start (tính cả tag + length), hoặc null nếu dữ liệu
    /// chưa đủ. Khác TryReadTlv (dễ dãi với dữ liệu cụt để quét bản ghi): hàm này chặt, dùng để biết đã
    /// nhận đủ PDU chưa. Hỗ trợ độ dài bất định (0x80 … 00 00) lồng nhau.</summary>
    public static int? MeasureTlv(ReadOnlySpan<byte> data, int start)
    {
        var pos = start;
        if (pos >= data.Length) return null;
        var first = data[pos++];
        if ((first & 0x1F) == 0x1F)
        {
            do { if (pos >= data.Length) return null; } while ((data[pos++] & 0x80) != 0);
        }
        if (pos >= data.Length) return null;
        var lenByte = data[pos++];
        if (lenByte == 0x80)
        {
            while (true)
            {
                if (pos + 1 >= data.Length) return null;
                if (data[pos] == 0 && data[pos + 1] == 0) return pos + 2 - start;
                var inner = MeasureTlv(data, pos);
                if (inner is null) return null;
                pos += inner.Value;
            }
        }
        long len = lenByte;
        if ((lenByte & 0x80) != 0)
        {
            var n = lenByte & 0x7F;
            if (n > 4) return null;
            len = 0;
            for (var i = 0; i < n; i++)
            {
                if (pos >= data.Length) return null;
                len = (len << 8) | data[pos++];
            }
        }
        var end = pos + len;
        return end <= data.Length ? (int)(end - start) : null;
    }

    // ── TCP transport ────────────────────────────────────────────────────────

    private static async Task SendPdu(NetworkStream stream, byte[] pdu)
    {
        // Z39.50 PDUs are sent as-is; no framing header needed (unlike some implementations)
        Console.WriteLine($"[Z3950BER] SEND ({pdu.Length}B) {Convert.ToHexString(pdu)}");
        await stream.WriteAsync(pdu);
        await stream.FlushAsync();
    }

    /// <summary>
    /// Đọc trọn 1 PDU. KHÔNG được dừng khi `DataAvailable` tạm thời = false: PresentResponse nhiều bản ghi
    /// (vài chục KB) tới thành nhiều gói TCP, dừng sớm sẽ cắt cụt PDU và làm rơi bản ghi (trang 10 chỉ
    /// còn 2–9 bản ghi). Đọc tới khi đủ đúng 1 TLV ngoài cùng theo độ dài BER (xác định hoặc bất định),
    /// hoặc server đóng kết nối / hết thời gian chờ.
    /// </summary>
    private static async Task<byte[]> ReadPdu(NetworkStream stream, CancellationToken ct)
    {
        var buffer = new byte[65536];
        var result = new List<byte>();
        stream.ReadTimeout = 5000;
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(buffer, ct);
                if (read == 0) break;
                result.AddRange(buffer[..read]);
                if (MeasureTlv(CollectionsMarshal.AsSpan(result), 0) is not null) break;
            }
        }
        catch (Exception) { /* timeout or closed */ }
        var data = result.ToArray();
        Console.WriteLine($"[Z3950BER] RECV ({data.Length}B) {Convert.ToHexString(data)}");
        return data;
    }
}
