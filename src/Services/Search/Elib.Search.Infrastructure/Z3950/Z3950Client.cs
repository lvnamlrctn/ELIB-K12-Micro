using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Elib.Search.Application;
using Elib.Search.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elib.Search.Infrastructure.Z3950;

public sealed class Z3950Options
{
    public const string SectionName = "Z3950";

    /// <summary>
    /// Cho kết nối tới địa chỉ nội bộ (10.x, 172.16–31.x, 192.168.x, loopback…). Mặc định KHÔNG: cán bộ đơn vị tự khai host/cổng, nếu cho
    /// phép thì có thể dùng search dò dịch vụ nội bộ (postgres, rabbitmq…). Chỉ bật khi máy chủ Z39.50 của sở/trường nằm trong mạng LAN.
    /// </summary>
    public bool AllowPrivateNetworks { get; set; }

    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Một phản hồi (PDU, trang SRU) lớn hơn mức này bị bỏ — chặn máy chủ lạ gửi dữ liệu vô hạn.</summary>
    public int MaxResponseBytes { get; set; } = 8 * 1024 * 1024;
}

/// <summary>Lỗi có câu báo cho người dùng (máy chủ từ chối, địa chỉ bị chặn…).</summary>
#pragma warning disable CA1032 // chỉ dùng nội bộ với câu báo tiếng Việt
internal sealed class Z3950Exception(string message) : Exception(message);
#pragma warning restore CA1032

/// <summary>
/// Tra Z39.50 (Init → Search → Present, BIB-1, RPN) hoặc SRU 1.1 (searchRetrieve, CQL, MARCXML). Mỗi lần gọi là một phiên mới, đóng ngay.
/// Kết nối chỉ tới địa chỉ công khai (xem <see cref="Z3950Options.AllowPrivateNetworks"/>) — kiểm tra trên IP đã phân giải, không trên tên.
/// </summary>
public sealed partial class Z3950Client(IHttpClientFactory httpFactory, IOptions<Z3950Options> options, ILogger<Z3950Client> logger) : IZ3950Client
{
    public const string SruHttpClient = "z3950-sru";

    private static readonly Dictionary<string, int> Bib1Use = new(StringComparer.OrdinalIgnoreCase)
    {
        ["title"] = 4, ["author"] = 1003, ["publisher"] = 1018, ["isbn"] = 7, ["keyword"] = 1016, ["subject"] = 21,
    };

    private static readonly Dictionary<string, string> CqlIndex = new(StringComparer.OrdinalIgnoreCase)
    {
        ["title"] = "dc.title", ["author"] = "dc.creator", ["publisher"] = "dc.publisher", ["isbn"] = "bath.isbn", ["keyword"] = "cql.serverChoice",
        ["subject"] = "dc.subject",
    };

    private readonly Z3950Options _options = options.Value;

    public async Task<Z3950FetchResult> SearchAsync(Z3950Target target, IReadOnlyList<Z3950Term> terms, int start, int count, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(terms);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        try
        {
            return target.SruUrl is { } url
                ? await SruSearchAsync(url, terms, start, count, timeout.Token)
                : await BerSearchAsync(target, terms, start, count, timeout.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            LogFailed(logger, target.SruUrl ?? target.Host, target.Port, target.Database, ex);
            return new Z3950FetchResult(false, 0, [], Friendly(ex));
        }
    }

    public async Task<string?> TestAsync(Z3950Target target, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(target);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        try
        {
            if (target.SruUrl is { } url)
            {
                var xml = await GetSruAsync(new Uri(url + (url.Contains('?', StringComparison.Ordinal) ? "&" : "?") + "operation=explain&version=1.1"), timeout.Token);
                _ = Parse(xml);
                return null;
            }
            using var session = await BerSession.OpenAsync(this, target, timeout.Token);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return Friendly(ex);
        }
    }

    private static string Friendly(Exception ex) => ex switch
    {
        Z3950Exception => ex.Message,
        OperationCanceledException => "Máy chủ không phản hồi (quá thời gian chờ).",
        SocketException s when s.SocketErrorCode == SocketError.HostNotFound => "Không tìm thấy máy chủ (sai địa chỉ?).",
        SocketException s when s.SocketErrorCode == SocketError.ConnectionRefused => "Máy chủ từ chối kết nối (sai cổng hoặc dịch vụ không chạy).",
        SocketException => "Không kết nối được máy chủ.",
        HttpRequestException h when h.StatusCode is { } code => $"Máy chủ SRU trả lỗi HTTP {(int)code}.",
        HttpRequestException => "Không kết nối được máy chủ SRU.",
        XmlException => "Máy chủ SRU trả dữ liệu không phải XML hợp lệ.",
        _ => "Lỗi khi tra máy chủ: " + ex.Message,
    };

    // ── Z39.50 ──────────────────────────────────────────────────────────────

    private async Task<Z3950FetchResult> BerSearchAsync(Z3950Target target, IReadOnlyList<Z3950Term> terms, int start, int count, CancellationToken ct)
    {
        using var session = await BerSession.OpenAsync(this, target, ct);
        var response = await session.ExchangeAsync(SearchRequest(target.Database, terms), ct);
        var (total, searchError) = ParseSearchResponse(response);
        if (searchError is not null) return new Z3950FetchResult(true, 0, [], searchError);
        if (total == 0 || start > total) return new Z3950FetchResult(true, total, [], null);

        var present = await session.ExchangeAsync(PresentRequest(start, Math.Min(count, total - start + 1), target.RecordSyntax), ct);
        var records = new List<Z3950Record>();
        CollectRecords(present, records);
        return new Z3950FetchResult(true, total, records, records.Count == 0 ? PresentError(present) : null);
    }

    /// <summary>Một phiên TCP: kết nối tới IP đã kiểm tra, gửi InitializeRequest, nhận InitializeResponse.</summary>
    private sealed class BerSession : IDisposable
    {
        private readonly Socket _socket;
        private readonly NetworkStream _stream;
        private readonly int _maxBytes;

        private BerSession(Socket socket, int maxBytes)
        {
            _socket = socket;
            _stream = new NetworkStream(socket, ownsSocket: false);
            _maxBytes = maxBytes;
        }

        public static async Task<BerSession> OpenAsync(Z3950Client client, Z3950Target target, CancellationToken ct)
        {
            var address = await ResolveAsync(target.Host, client._options.AllowPrivateNetworks, ct);
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            BerSession? session = null;
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, target.Port), ct);
                session = new BerSession(socket, client._options.MaxResponseBytes);
                var init = await session.ExchangeAsync(InitRequest(target.UserName, target.Password), ct);
                if (!InitAccepted(init)) throw new Z3950Exception("Máy chủ từ chối phiên Z39.50 (sai tên đăng nhập/mật khẩu?).");
                return session;
            }
            catch
            {
                if (session is not null) session.Dispose();
                else socket.Dispose();
                throw;
            }
        }

        public async Task<byte[]> ExchangeAsync(byte[] pdu, CancellationToken ct)
        {
            await _stream.WriteAsync(pdu, ct);
            await _stream.FlushAsync(ct);
            using var received = new MemoryStream();
            var buffer = new byte[65536];
            while (true)
            {
                var read = await _stream.ReadAsync(buffer, ct);
                if (read == 0) break;
                received.Write(buffer, 0, read);
                if (received.Length > _maxBytes) throw new Z3950Exception("Máy chủ trả dữ liệu quá lớn.");
                if (Ber.Measure(received.GetBuffer().AsSpan(0, (int)received.Length), 0) is not null) break;
            }
            if (received.Length == 0) throw new Z3950Exception("Máy chủ đóng kết nối, không trả lời.");
            return received.ToArray();
        }

        public void Dispose()
        {
            _stream.Dispose();
            _socket.Dispose();
        }
    }

    private static byte[] InitRequest(string? user, string? password)
    {
        // InitializeRequest: protocolVersion [3], options [4] (search, present), preferredMessageSize [5], exceptionalRecordSize [6],
        // idAuthentication [7] (idPass), implementationName [111].
        var body = new List<byte[]>
        {
            Ber.Context(3, Ber.BitStringRaw(0b11100000)),
            Ber.Context(4, Ber.BitStringRaw(0b11000000)),
            Ber.Context(5, Ber.IntRaw(1_048_576)),
            Ber.Context(6, Ber.IntRaw(1_048_576)),
        };
        if (!string.IsNullOrEmpty(user))
            body.Add(Ber.ContextConstructed(7, Ber.Sequence(Ber.Context(1, Ber.OctetRaw(user)), Ber.Context(2, Ber.OctetRaw(password ?? "")))));
        body.Add(Ber.Context(111, Ber.OctetRaw("ELIB K12 Z39.50")));
        return Ber.Pdu(20, Ber.Concat([.. body]));
    }

    private static byte[] SearchRequest(string database, IReadOnlyList<Z3950Term> terms)
    {
        // RPN: các điều kiện nối AND — [1]{ rpn1, rpn2, [46]{[0] and} }; mỗi điều kiện [0]{ [102]{ [44]{ SEQ{[120] 1, [121] use} }, [45] term } }.
        byte[] Operand(Z3950Term t) => Ber.ContextConstructed(0, Ber.ContextConstructed(102, Ber.Concat(
            Ber.ContextConstructed(44, Ber.Sequence(Ber.Context(120, Ber.IntRaw(1)), Ber.Context(121, Ber.IntRaw(Bib1Use.GetValueOrDefault(t.Field, 1016))))),
            Ber.Context(45, Ber.OctetRaw(t.Value)))));
        var rpn = Operand(terms[0]);
        for (var i = 1; i < terms.Count; i++)
            rpn = Ber.ContextConstructed(1, Ber.Concat(rpn, Operand(terms[i]), Ber.ContextConstructed(46, Ber.Context(0, []))));
        var query = Ber.ContextConstructed(1, Ber.Concat(Ber.Oid(1, 2, 840, 10003, 3, 1), rpn)); // type-1, attributeSet BIB-1

        return Ber.Pdu(22, Ber.Concat(
            Ber.Context(13, Ber.IntRaw(0)),
            Ber.Context(14, Ber.IntRaw(1)),
            Ber.Context(15, Ber.IntRaw(0)),
            Ber.Context(16, Ber.BoolRaw(true)),
            Ber.Context(17, Ber.OctetRaw("default")),
            Ber.ContextConstructed(18, Ber.Context(105, Ber.OctetRaw(database))),
            Ber.ContextConstructed(21, query)));
    }

    private static byte[] PresentRequest(int start, int count, string syntax) => Ber.Pdu(24, Ber.Concat(
        Ber.Context(31, Ber.OctetRaw("default")),
        Ber.Context(30, Ber.IntRaw(start)),
        Ber.Context(29, Ber.IntRaw(count)),
        // preferredRecordSyntax: USMARC 1.2.840.10003.5.10, UNIMARC 1.2.840.10003.5.1
        Ber.Context(104, Ber.OidRaw(1, 2, 840, 10003, 5, syntax == "UNIMARC" ? 1 : 10))));

    private static bool InitAccepted(byte[] pdu)
    {
        var pos = 0;
        if (!Ber.TryRead(pdu, ref pos, out var outer) || outer.Tag != 21) return false;
        // result [12] IMPLICIT BOOLEAN — vài máy chủ bỏ field này khi chấp nhận.
        var result = Ber.Children(outer.Content).FirstOrDefault(e => e.Class == 2 && e.Tag == 12 && !e.Constructed);
        return result.Content is null || result.Content.Length == 0 || result.Content[0] != 0;
    }

    private static (int Total, string? Error) ParseSearchResponse(byte[] pdu)
    {
        var pos = 0;
        if (!Ber.TryRead(pdu, ref pos, out var outer) || outer.Tag != 23) throw new Z3950Exception("Máy chủ trả lời tìm kiếm không hợp lệ.");
        int? total = null;
        bool? status = null;
        string? diagnostic = null;
        foreach (var e in Ber.Children(outer.Content).Where(e => e.Class == 2))
        {
            if (e.Tag == 23 && !e.Constructed) total = Ber.ToInt(e.Content);
            else if (e.Tag == 22 && !e.Constructed) status = e.Content.Length > 0 && e.Content[0] != 0;
            else if (e.Tag is 130 or 205) diagnostic = Diagnostic(e.Content);
        }
        if (status == false || total is null) return (0, diagnostic ?? "Máy chủ không thực hiện được câu tìm.");
        return (total.Value, null);
    }

    private static string? PresentError(byte[] pdu)
    {
        var pos = 0;
        if (!Ber.TryRead(pdu, ref pos, out var outer)) return "Máy chủ trả bản ghi không hợp lệ.";
        foreach (var e in Ber.Children(outer.Content).Where(e => e.Class == 2))
            if (e.Tag is 130 or 205 or 28) return Diagnostic(e.Content) ?? "Máy chủ không trả được bản ghi.";
        return "Máy chủ không trả bản ghi MARC (khổ mẫu không hỗ trợ?).";
    }

    /// <summary>Mã chẩn đoán Bib-1 (INTEGER đầu tiên trong DefaultDiagFormat) kèm addinfo nếu có.</summary>
    private static string? Diagnostic(byte[] content)
    {
        int? code = null;
        string? info = null;
        void Walk(byte[] data)
        {
            foreach (var e in Ber.Children(data))
            {
                if (e.Class == 0 && e.Tag == 2 && code is null) code = Ber.ToInt(e.Content);
                else if (e.Class == 0 && e.Tag is 26 or 27 or 12 && info is null) info = Encoding.UTF8.GetString(e.Content);
                else if (e.Constructed) Walk(e.Content);
            }
        }
        Walk(content);
        if (code is null) return null;
        var meaning = code switch
        {
            13 => "yêu cầu ngoài phạm vi / tạm bị giới hạn",
            109 or 235 => "không có cơ sở dữ liệu này",
            114 => "máy chủ không hỗ trợ tìm theo trường này",
            238 or 239 => "máy chủ không hỗ trợ khổ mẫu bản ghi đã chọn",
            _ => null,
        };
        return string.Create(CultureInfo.InvariantCulture,
            $"Máy chủ báo lỗi (Bib-1 {code}{(meaning is null ? "" : ": " + meaning)}{(string.IsNullOrWhiteSpace(info) ? "" : " — " + info)}).");
    }

    /// <summary>
    /// Bản ghi trong PresentResponse nằm sâu trong NamePlusRecord → EXTERNAL → octet-aligned; quét đệ quy mọi phần tử, nhận phần tử có
    /// nội dung giống ISO2709 (5 chữ số đầu = độ dài bản ghi) — bền hơn bóc đúng từng tầng EXTERNAL (cách của monolith, đã chạy với LOC).
    /// </summary>
    private static void CollectRecords(byte[] data, List<Z3950Record> records)
    {
        foreach (var e in Ber.Children(data))
        {
            if (LooksLikeIso2709(e.Content))
            {
                if (Z3950Marc.ReadIso2709(e.Content) is { } record) records.Add(record);
            }
            else if (e.Constructed) CollectRecords(e.Content, records);
        }
    }

    private static bool LooksLikeIso2709(byte[] data)
    {
        if (data.Length < 25 || data.AsSpan(0, 5).IndexOfAnyExceptInRange((byte)'0', (byte)'9') >= 0) return false;
        var declared = int.Parse(Encoding.ASCII.GetString(data, 0, 5), CultureInfo.InvariantCulture);
        return declared > 24 && Math.Abs(declared - data.Length) <= 2;
    }

    // ── SRU ─────────────────────────────────────────────────────────────────

    private async Task<Z3950FetchResult> SruSearchAsync(string url, IReadOnlyList<Z3950Term> terms, int start, int count, CancellationToken ct)
    {
        var cql = string.Join(" and ", terms.Select(t => $"{CqlIndex.GetValueOrDefault(t.Field, "cql.serverChoice")}=\"{t.Value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\""));
        var query = string.Create(CultureInfo.InvariantCulture,
            $"operation=searchRetrieve&version=1.1&query={Uri.EscapeDataString(cql)}&startRecord={start}&maximumRecords={count}&recordSchema=marcxml&recordPacking=xml");
        var xml = await GetSruAsync(new Uri(url + (url.Contains('?', StringComparison.Ordinal) ? "&" : "?") + query), ct);
        var doc = Parse(xml);
        var total = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "numberOfRecords") is { } n
            && int.TryParse(n.Value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var t) ? t : 0;
        var message = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "diagnostic")?.Descendants()
            .FirstOrDefault(e => e.Name.LocalName is "message" or "details")?.Value;
        var records = Z3950Marc.ReadMarcXml(xml);
        return new Z3950FetchResult(true, total, records,
            records.Count == 0 && message is not null ? $"Máy chủ SRU báo lỗi: {message}" : null);
    }

    private async Task<string> GetSruAsync(Uri uri, CancellationToken ct)
    {
        // Kiểm tra trước khi gọi (ConnectCallback của HttpClient kiểm tra lại trên IP thật, kể cả khi bị chuyển hướng).
        await ResolveAsync(uri.Host, _options.AllowPrivateNetworks, ct);
        using var http = httpFactory.CreateClient(SruHttpClient);
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > _options.MaxResponseBytes) throw new Z3950Exception("Máy chủ SRU trả dữ liệu quá lớn.");
        await using var body = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[65536];
        int read;
        while ((read = await body.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > _options.MaxResponseBytes) throw new Z3950Exception("Máy chủ SRU trả dữ liệu quá lớn.");
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static XDocument Parse(string xml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(new StringReader(xml), settings);
        return XDocument.Load(reader);
    }

    // ── Chặn địa chỉ nội bộ ─────────────────────────────────────────────────

    /// <summary>IP để kết nối (ưu tiên IPv4). Bị chặn nếu BẤT KỲ địa chỉ nào của tên miền là nội bộ (chống DNS trỏ một phần vào mạng trong).</summary>
    internal static async Task<IPAddress> ResolveAsync(string host, bool allowPrivate, CancellationToken ct)
    {
        var addresses = IPAddress.TryParse(host, out var literal) ? [literal] : await Dns.GetHostAddressesAsync(host, ct);
        if (addresses.Length == 0) throw new Z3950Exception("Không tìm thấy máy chủ (sai địa chỉ?).");
        if (!allowPrivate && addresses.Any(a => !IsPublic(a)))
            throw new Z3950Exception("Địa chỉ máy chủ thuộc mạng nội bộ — không được phép kết nối.");
        return addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses[0];
    }

    /// <summary>Kiểm tra lại trên socket thật của HttpClient (SRU) — IP đã phân giải lúc kết nối, kể cả sau chuyển hướng.</summary>
    public static async ValueTask<Stream> ConnectGuardedAsync(SocketsHttpConnectionContext context, bool allowPrivate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var address = await ResolveAsync(context.DnsEndPoint.Host, allowPrivate, ct);
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public static bool IsPublic(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return false;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] is 0 or 10 or 127 || b[0] >= 224
                     || (b[0] == 100 && b[1] is >= 64 and <= 127) // CGNAT
                     || (b[0] == 169 && b[1] == 254)
                     || (b[0] == 172 && b[1] is >= 16 and <= 31)
                     || (b[0] == 192 && b[1] == 168)
                     || (b[0] == 192 && b[1] == 0 && b[2] == 0));
        }
        var bytes = address.GetAddressBytes();
        return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || (bytes[0] & 0xFE) == 0xFC);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Tra Z39.50/SRU {Target} (cổng {Port}, CSDL {Database}) lỗi")]
    private static partial void LogFailed(ILogger logger, string target, int port, string database, Exception exception);
}
