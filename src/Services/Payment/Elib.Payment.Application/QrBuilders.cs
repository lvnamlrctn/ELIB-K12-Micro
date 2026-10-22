using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Elib.Payment.Application;

/// <summary>
/// Chuỗi QR VietQR (chuẩn EMVCo + Napas 247): người dùng quét bằng app ngân hàng, số tiền và nội dung chuyển khoản đã điền sẵn —
/// nội dung chính là mã giao dịch để Sepay báo về đúng khoản. Không gọi dịch vụ ngoài.
/// </summary>
public static class VietQrPayload
{
    public static string Build(string bankBin, string accountNo, decimal amount, string note, string? accountName = null)
    {
        var account = Field("00", "A000000727") + Field("01", Field("00", bankBin) + Field("01", accountNo)) + Field("02", "QRIBFTTA");
        var sb = new StringBuilder();
        sb.Append(Field("00", "01")).Append(Field("01", "12")).Append(Field("38", account)).Append(Field("53", "704"))
          .Append(Field("54", decimal.Truncate(amount).ToString(CultureInfo.InvariantCulture))).Append(Field("58", "VN"));
        if (Fold(accountName) is { Length: > 0 } name) sb.Append(Field("59", name.Length <= 25 ? name : name[..25])).Append(Field("60", "ELIB")); // đúng bố cục monolith đã chạy thật với app ngân hàng
        var info = Fold(note);
        sb.Append(Field("62", Field("08", info.Length <= 25 ? info : info[..25])));
        sb.Append("6304");
        return sb.Append(Crc16(sb.ToString()).ToString("X4", CultureInfo.InvariantCulture)).ToString();
    }

    /// <summary>CRC16-CCITT-FALSE (đa thức 0x1021, khởi tạo 0xFFFF) trên toàn chuỗi gồm cả "6304".</summary>
    public static ushort Crc16(string data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.ASCII.GetBytes(data))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++) crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }

    private static string Field(string id, string value) => id + value.Length.ToString("D2", CultureInfo.InvariantCulture) + value;

    /// <summary>Bỏ dấu tiếng Việt, chỉ giữ ASCII in được (QR ngân hàng không nhận Unicode).</summary>
    public static string Fold(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var normalized = text.Trim().Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && c is >= ' ' and <= '~') sb.Append(c);
        return sb.ToString();
    }
}

/// <summary>Địa chỉ thanh toán VNPAY (v2.1.0): tham số sắp theo tên, giá trị mã hoá URL, ký HMAC-SHA512 bằng HashSecret của đơn vị.</summary>
public static class VnPayUrl
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static string Build(string paymentUrl, string tmnCode, string hashSecret, string txnRef, decimal amount, string orderInfo, string returnUrl,
        string ipAddress, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        var p = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0", ["vnp_Command"] = "pay", ["vnp_TmnCode"] = tmnCode,
            ["vnp_Amount"] = (decimal.Truncate(amount) * 100).ToString(CultureInfo.InvariantCulture),
            ["vnp_CreateDate"] = Stamp(createdAt), ["vnp_ExpireDate"] = Stamp(expiresAt), ["vnp_CurrCode"] = "VND", ["vnp_IpAddr"] = ipAddress,
            ["vnp_Locale"] = "vn", ["vnp_OrderInfo"] = PlainOrderInfo(orderInfo), ["vnp_OrderType"] = "other", ["vnp_ReturnUrl"] = returnUrl,
            ["vnp_TxnRef"] = txnRef,
        };
        var query = Query(p);
        return paymentUrl + "?" + query + "&vnp_SecureHash=" + Sign(query, hashSecret);
    }

    /// <summary>VNPAY cấm ký tự đặc biệt ở mô tả đơn: bỏ dấu, chỉ giữ chữ, số và khoảng trắng (tối đa 255).</summary>
    public static string PlainOrderInfo(string text)
    {
        var spaced = new string((text ?? "").Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray()); // dấu câu, gạch, × … → khoảng trắng (trước khi bỏ dấu để "1–10" không dính thành "110")
        var chars = VietQrPayload.Fold(spaced).Select(c => char.IsAsciiLetterOrDigit(c) ? c : ' ').ToArray();
        var plain = string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return plain.Length <= 255 ? plain : plain[..255];
    }

    public static string Stamp(DateTimeOffset at) => at.ToOffset(VietnamOffset).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

    /// <summary>Chuỗi tham số đã mã hoá — đúng chuỗi mà VNPAY băm để ký.</summary>
    public static string Query(IEnumerable<KeyValuePair<string, string>> parameters) =>
        string.Join('&', parameters.Where(kv => kv.Value.Length > 0).OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => System.Net.WebUtility.UrlEncode(kv.Key) + "=" + System.Net.WebUtility.UrlEncode(kv.Value))); // đúng như thư viện mẫu của VNPAY

    public static string Sign(string query, string hashSecret)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(hashSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(query))).ToLowerInvariant();
    }

    /// <summary>Kiểm chữ ký IPN/return: bỏ vnp_SecureHash / vnp_SecureHashType, ký lại, so sánh hằng thời gian.</summary>
    public static bool Verify(IReadOnlyDictionary<string, string> parameters, string hashSecret)
    {
        if (!parameters.TryGetValue("vnp_SecureHash", out var given) || string.IsNullOrWhiteSpace(given)) return false;
        var query = Query(parameters.Where(kv => kv.Key.StartsWith("vnp_", StringComparison.Ordinal) && kv.Key is not ("vnp_SecureHash" or "vnp_SecureHashType")));
        var expected = Sign(query, hashSecret);
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(given.Trim().ToLowerInvariant()));
    }
}
