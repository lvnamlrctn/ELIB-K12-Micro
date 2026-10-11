using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace Elib.BuildingBlocks.Hosting;

/// <summary>Địa chỉ đích bị chặn (mạng nội bộ) hoặc không phân giải được — câu báo dùng được cho người dùng.</summary>
#pragma warning disable CA1032 // chỉ tạo với câu báo tiếng Việt
public sealed class BlockedAddressException(string message) : Exception(message);
#pragma warning restore CA1032

/// <summary>
/// Chặn service gọi ra địa chỉ do người dùng nhập (máy chủ Z39.50, ảnh bìa theo URL…) lọt vào mạng nội bộ (SSRF): kiểm tra trên IP ĐÃ PHÂN
/// GIẢI (không trên tên miền) và kết nối thẳng tới IP đó — tên miền trỏ một phần vào mạng trong, hay đổi IP giữa lúc kiểm và lúc nối
/// (DNS rebinding), đều không lọt. Dùng <see cref="Handler"/> cho HttpClient: kiểm cả khi bị chuyển hướng.
/// </summary>
public static class PublicNetwork
{
    /// <summary>Địa chỉ Internet công khai: không phải loopback, 10/8, 172.16/12, 192.168/16, 169.254/16, 100.64/10, multicast, IPv6 ULA/link-local…</summary>
    public static bool IsPublic(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return false;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] is 0 or 10 or 127 || b[0] >= 224
                     || (b[0] == 100 && b[1] is >= 64 and <= 127)
                     || (b[0] == 169 && b[1] == 254)
                     || (b[0] == 172 && b[1] is >= 16 and <= 31)
                     || (b[0] == 192 && b[1] == 168)
                     || (b[0] == 192 && b[1] == 0 && b[2] == 0));
        }
        var bytes = address.GetAddressBytes();
        return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || (bytes[0] & 0xFE) == 0xFC);
    }

    /// <summary>IP để kết nối (ưu tiên IPv4). <paramref name="allowPrivate"/> = false: chặn nếu BẤT KỲ địa chỉ nào của tên miền là nội bộ.</summary>
    public static async Task<IPAddress> ResolveAsync(string host, bool allowPrivate, CancellationToken ct)
    {
        IPAddress[] addresses;
        try
        {
            addresses = IPAddress.TryParse(host, out var literal) ? [literal] : await Dns.GetHostAddressesAsync(host, ct);
        }
        catch (SocketException)
        {
            throw new BlockedAddressException("Không tìm thấy máy chủ (sai địa chỉ?).");
        }
        if (addresses.Length == 0) throw new BlockedAddressException("Không tìm thấy máy chủ (sai địa chỉ?).");
        if (!allowPrivate && addresses.Any(a => !IsPublic(a)))
            throw new BlockedAddressException("Địa chỉ máy chủ thuộc mạng nội bộ — không được phép kết nối.");
        return addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses[0];
    }

    /// <summary>Handler cho HttpClient gọi ra Internet: mọi kết nối (kể cả sau chuyển hướng) đi qua kiểm tra IP.</summary>
    public static SocketsHttpHandler Handler(bool allowPrivate, int maxRedirects = 3) => new()
    {
        ConnectCallback = (context, ct) => ConnectAsync(context, allowPrivate, ct),
        MaxAutomaticRedirections = maxRedirects,
    };

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, bool allowPrivate, CancellationToken ct)
    {
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
}
