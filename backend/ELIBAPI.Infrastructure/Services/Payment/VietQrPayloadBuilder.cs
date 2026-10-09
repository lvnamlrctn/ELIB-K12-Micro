using System.Text;

namespace ELIBAPI.Infrastructure.Services.Payment;

/// <summary>Dựng chuỗi QR theo chuẩn VietQR/EMVCo (dùng chung bởi mọi app ngân hàng VN) — không cần
/// gọi API ngoài, frontend render thẳng chuỗi này thành ảnh QR (thư viện `qrcode` npm sẵn có).</summary>
public static class VietQrPayloadBuilder
{
    private const string NapasAid = "A000000727";

    public static string Build(string bankBin, string accountNo, string accountName, double amount, string content)
    {
        var sb = new StringBuilder();
        Tlv(sb, "00", "01");                       // Payload Format Indicator
        Tlv(sb, "01", "12");                       // Point of Initiation Method = dynamic (1 lần)

        var merchantAccount = new StringBuilder();
        Tlv(merchantAccount, "00", NapasAid);
        var beneficiary = new StringBuilder();
        Tlv(beneficiary, "00", bankBin);
        Tlv(beneficiary, "01", accountNo);
        Tlv(merchantAccount, "01", beneficiary.ToString());
        Tlv(merchantAccount, "02", "QRIBFTTA");     // chuyển khoản tới số tài khoản
        Tlv(sb, "38", merchantAccount.ToString());

        Tlv(sb, "53", "704");                       // VND
        Tlv(sb, "54", amount.ToString("0"));         // không thập phân với VND
        Tlv(sb, "58", "VN");
        Tlv(sb, "59", Truncate(RemoveDiacritics(accountName), 25));
        Tlv(sb, "60", "ELIB");

        var additional = new StringBuilder();
        Tlv(additional, "08", Truncate(content, 25));
        Tlv(sb, "62", additional.ToString());

        sb.Append("6304"); // tag CRC + length cố định, giá trị CRC tính ngay sau
        var crc = Crc16Ccitt(sb.ToString());
        sb.Append(crc.ToString("X4"));
        return sb.ToString();
    }

    private static void Tlv(StringBuilder sb, string tag, string value)
    {
        sb.Append(tag).Append(value.Length.ToString("00")).Append(value);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private static string RemoveDiacritics(string s)
    {
        var normalized = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Replace('đ', 'd').Replace('Đ', 'D');
    }

    public static ushort Crc16Ccitt(string input)
    {
        var bytes = Encoding.ASCII.GetBytes(input);
        ushort crc = 0xFFFF;
        foreach (var b in bytes)
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }
}
