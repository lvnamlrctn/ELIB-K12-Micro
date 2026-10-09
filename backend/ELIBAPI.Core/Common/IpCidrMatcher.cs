using System.Net;

namespace ELIBAPI.Core.Common;

/// <summary>Đợt 22.3 — port từ ELIB-LRC, không đổi gì. So khớp 1 địa chỉ IP với danh sách dải CIDR
/// (vd "10.20.0.0/16") — dùng để giới hạn IP gọi api/public/oai (theo từng đơn vị — xem
/// OaiPmhController). Hỗ trợ cả IPv4 và IPv6.</summary>
public static class IpCidrMatcher
{
    /// <summary>true nếu <paramref name="cidrs"/> rỗng (không giới hạn) hoặc ip khớp ít nhất 1 dải.</summary>
    public static bool IsAllowed(string? ip, IReadOnlyCollection<string> cidrs)
    {
        if (cidrs.Count == 0) return true;
        if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip, out var addr)) return false;

        foreach (var cidr in cidrs)
            if (Matches(addr, cidr))
                return true;
        return false;
    }

    private static bool Matches(IPAddress addr, string cidr)
    {
        var parts = cidr.Split('/', 2);
        if (!IPAddress.TryParse(parts[0].Trim(), out var network)) return false;
        if (network.AddressFamily != addr.AddressFamily) return false;

        var networkBytes = network.GetAddressBytes();
        var addrBytes    = addr.GetAddressBytes();
        var prefixLen = parts.Length == 2 && int.TryParse(parts[1], out var p) ? p : networkBytes.Length * 8;
        prefixLen = Math.Clamp(prefixLen, 0, networkBytes.Length * 8);

        var fullBytes = prefixLen / 8;
        for (var i = 0; i < fullBytes; i++)
            if (networkBytes[i] != addrBytes[i]) return false;

        var remainingBits = prefixLen % 8;
        if (remainingBits == 0) return true;

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (networkBytes[fullBytes] & mask) == (addrBytes[fullBytes] & mask);
    }
}
