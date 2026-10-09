using System.Net;
using System.Net.Sockets;
using Elib.BuildingBlocks.Domain;
using Microsoft.Extensions.Options;

namespace Elib.Notification.Application;

/// <summary>
/// Chặn SMTP của đơn vị trỏ vào mạng nội bộ (SSRF qua nút "Gửi thử"). Kiểm tra khi lưu cấu hình và ngay trước khi gửi.
/// Còn khe hở DNS rebinding giữa lúc kiểm tra và lúc MailKit tự phân giải lại — chấp nhận vì cổng SMTP không trả nội dung về cho người gọi.
/// </summary>
public sealed class SmtpHostPolicy(IOptions<NotificationOptions> options)
{
    public async Task EnsureAllowedAsync(string host, CancellationToken cancellationToken)
    {
        if (options.Value.AllowPrivateSmtpHosts) return;

        IPAddress[] addresses;
        if (IPAddress.TryParse(host, out var literal))
        {
            addresses = [literal];
        }
        else
        {
            try
            {
                addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            }
            catch (SocketException)
            {
                throw new BusinessRuleException("SMTP_HOST_UNRESOLVED", $"Không phân giải được máy chủ SMTP '{host}'.");
            }
        }

        if (addresses.Length == 0 || addresses.Any(IsPrivate))
            throw new BusinessRuleException("SMTP_HOST_NOT_ALLOWED", "Máy chủ SMTP phải là địa chỉ công khai, không được trỏ vào mạng nội bộ.");
    }

    public static bool IsPrivate(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return true;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal || address.IsIPv6Multicast;

        var b = address.GetAddressBytes();
        return b[0] switch
        {
            0 or 10 or 127 => true,
            100 => b[1] is >= 64 and <= 127,   // CGNAT 100.64.0.0/10
            169 => b[1] == 254,                // link-local (metadata của cloud)
            172 => b[1] is >= 16 and <= 31,
            192 => b[1] == 168,
            >= 224 => true,                    // multicast + dự phòng
            _ => false,
        };
    }
}
