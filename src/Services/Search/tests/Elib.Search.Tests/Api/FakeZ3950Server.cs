using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Elib.Search.Tests.Api;

/// <summary>
/// Máy chủ Z39.50 giả trên 127.0.0.1 (cổng ngẫu nhiên): trả InitializeResponse, SearchResponse (tổng = số bản ghi), PresentResponse bọc
/// bản ghi ISO2709 trong NamePlusRecord → EXTERNAL như YAZ. Ghi lại các PDU nhận được để kiểm tra câu tìm.
/// </summary>
public sealed class FakeZ3950Server : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    public FakeZ3950Server(params byte[][] records)
    {
        Records = [.. records];
        _listener.Start();
        _loop = AcceptLoopAsync();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public List<byte[]> Records { get; }
    public ConcurrentQueue<byte[]> Requests { get; } = new();
    public bool RejectInit { get; set; }

    /// <summary>Bản ghi MARC21 ISO2709 UTF-8 tối thiểu: 245$a nhan đề, 100$a tác giả, 260$b$c, 020$a.</summary>
    public static byte[] Iso2709(string title, string author, string? isbn = null)
    {
        var fields = new List<(string Tag, byte[] Data)>
        {
            ("001", Encoding.ASCII.GetBytes("REMOTE-" + title.Length.ToString(CultureInfo.InvariantCulture))),
        };
        if (isbn is not null) fields.Add(("020", Data(("a", isbn))));
        fields.Add(("100", Data(("a", author))));
        fields.Add(("245", Data(("a", title + " /"), ("c", author))));
        fields.Add(("260", Data(("a", "H. :"), ("b", "Văn học,"), ("c", "2019"))));

        var directory = new StringBuilder();
        using var body = new MemoryStream();
        foreach (var (tag, data) in fields)
        {
            directory.Append(CultureInfo.InvariantCulture, $"{tag}{data.Length + 1:D4}{body.Length:D5}");
            body.Write(data);
            body.WriteByte(0x1E);
        }
        var baseAddress = 24 + directory.Length + 1;
        var length = baseAddress + (int)body.Length + 1;
        var leader = $"{length:D5}nam a22{baseAddress:D5} a 4500";
        return [.. Encoding.ASCII.GetBytes(leader), .. Encoding.ASCII.GetBytes(directory.ToString()), 0x1E, .. body.ToArray(), 0x1D];

        static byte[] Data(params (string Code, string Value)[] subfields)
        {
            var bytes = new List<byte> { (byte)'1', (byte)'0' };
            foreach (var (code, value) in subfields)
            {
                bytes.Add(0x1F);
                bytes.AddRange(Encoding.UTF8.GetBytes(code + value));
            }
            return [.. bytes];
        }
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }
            _ = ServeAsync(client);
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            var stream = client.GetStream();
            try
            {
                while (await ReadPduAsync(stream) is { } pdu)
                {
                    Requests.Enqueue(pdu);
                    var response = pdu[0] switch
                    {
                        0xB4 => Wrap(0xB5, [0x8C, 0x01, RejectInit ? (byte)0x00 : (byte)0xFF]),
                        0xB6 => Wrap(0xB7, [.. Int(0x97, Records.Count), .. Int(0x98, 0), .. Int(0x99, 1), 0x96, 0x01, 0xFF]),
                        0xB8 => Present(pdu),
                        _ => null,
                    };
                    if (response is null) return;
                    await stream.WriteAsync(response);
                }
            }
            catch (IOException)
            {
                // client đóng kết nối
            }
        }
    }

    private byte[] Present(byte[] pdu)
    {
        int start = 1, count = 1;
        var pos = HeaderLength(pdu);
        while (pos < pdu.Length)
        {
            var tag = pdu[pos++];
            if ((tag & 0x1F) == 0x1F) while ((pdu[pos++] & 0x80) != 0) { } // tag nhiều byte ([31] resultSetId, [104])
            var length = pdu[pos++];
            var value = 0;
            for (var i = 0; i < length && i < 4; i++) value = (value << 8) | pdu[pos + i];
            if (tag == 0x9E) start = value;
            if (tag == 0x9D) count = value;
            pos += length;
        }
        var records = new List<byte>();
        foreach (var iso in Records.Skip(start - 1).Take(count))
        {
            byte[] external = Tlv(0x28, [.. Tlv(0x06, [0x2A, 0x86, 0x48, 0xCE, 0x13, 0x05, 0x0A]), .. Tlv(0x81, iso)]);
            records.AddRange(Tlv(0x30, Tlv(0xA1, Tlv(0xA1, external))));
        }
        var returned = Math.Max(0, Math.Min(count, Records.Count - start + 1));
        return Wrap(0xB9, [.. Int(0x98, returned), .. Int(0x99, start + returned), 0x9B, 0x01, 0x00, .. Tlv(0xBC, [.. records])]);
    }

    private static byte[] Int(byte tag, int value) => value < 128 ? [tag, 0x01, (byte)value] : [tag, 0x02, (byte)(value >> 8), (byte)(value & 0xFF)];

    private static byte[] Wrap(byte tag, byte[] body) => Tlv(tag, body);

    private static byte[] Tlv(byte tag, byte[] content) => content.Length switch
    {
        < 128 => [tag, (byte)content.Length, .. content],
        < 256 => [tag, 0x81, (byte)content.Length, .. content],
        _ => [tag, 0x82, (byte)(content.Length >> 8), (byte)(content.Length & 0xFF), .. content],
    };

    private static int HeaderLength(byte[] pdu) => (pdu[1] & 0x80) == 0 ? 2 : 2 + (pdu[1] & 0x7F);

    private static async Task<byte[]?> ReadPduAsync(NetworkStream stream)
    {
        var header = new byte[2];
        if (!await FillAsync(stream, header)) return null;
        var lengthBytes = (header[1] & 0x80) == 0 ? 0 : header[1] & 0x7F;
        var extra = new byte[lengthBytes];
        if (!await FillAsync(stream, extra)) return null;
        var length = lengthBytes == 0 ? header[1] : extra.Aggregate(0, (acc, b) => (acc << 8) | b);
        var content = new byte[length];
        if (!await FillAsync(stream, content)) return null;
        return [.. header, .. extra, .. content];
    }

    private static async Task<bool> FillAsync(NetworkStream stream, byte[] buffer)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read));
            if (n == 0) return false;
            read += n;
        }
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        try
        {
            await _loop;
        }
        catch (OperationCanceledException)
        {
        }
        _listener.Dispose();
        _stop.Dispose();
    }
}
