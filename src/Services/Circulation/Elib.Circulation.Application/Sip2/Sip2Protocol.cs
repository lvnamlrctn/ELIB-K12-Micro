using System.Globalization;
using System.Text;

namespace Elib.Circulation.Application.Sip2;

/// <summary>
/// Một thông điệp SIP2 (3M SIP 2.00): mã lệnh 2 ký tự, phần cố định (độ dài theo lệnh), các trường biến đổi "XXgiá trị|" (có thể lặp,
/// ví dụ AU), tuỳ chọn AY (số thứ tự) + AZ (checksum). Viết lại gọn từ D:\code\SIP2 (SIP2.Protocol) của ELIB-LRC.
/// </summary>
public sealed class Sip2Message(string command, string fixedPart = "")
{
    public string Command { get; } = command;
    public string Fixed { get; } = fixedPart;
    public char? Sequence { get; init; }
    public List<KeyValuePair<string, string>> Fields { get; } = [];

    public string? Field(string tag) => Fields.FirstOrDefault(f => f.Key == tag).Value;

    public Sip2Message Add(string tag, string? value)
    {
        if (value is not null) Fields.Add(new(tag, value));
        return this;
    }

    /// <summary>Ký tự thứ <paramref name="index"/> của phần cố định (thiếu thì <paramref name="fallback"/>).</summary>
    public char FixedChar(int index, char fallback = ' ') => index < Fixed.Length ? Fixed[index] : fallback;
}

public static class Sip2Commands
{
    public const string BlockPatron = "01";
    public const string Checkin = "09";
    public const string Checkout = "11";
    public const string Hold = "15";
    public const string ItemInformation = "17";
    public const string ItemStatusUpdate = "19";
    public const string PatronStatus = "23";
    public const string PatronEnable = "25";
    public const string Renew = "29";
    public const string EndPatronSession = "35";
    public const string FeePaid = "37";
    public const string PatronInformation = "63";
    public const string RenewAll = "65";
    public const string Login = "93";
    public const string RequestAcsResend = "97";
    public const string ScStatus = "99";

    /// <summary>Độ dài phần cố định sau mã lệnh (SIP 2.00), cả lệnh của trạm lẫn thông điệp trả lời.</summary>
    public static readonly IReadOnlyDictionary<string, int> FixedLength = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [BlockPatron] = 19, [Checkin] = 37, [Checkout] = 38, [Hold] = 19, [ItemInformation] = 18, [ItemStatusUpdate] = 18,
        [PatronStatus] = 21, [PatronEnable] = 18, [Renew] = 38, [EndPatronSession] = 18, [FeePaid] = 25, [PatronInformation] = 31,
        [RenewAll] = 18, [Login] = 2, [RequestAcsResend] = 0, [ScStatus] = 8,
        // ACS → SC
        ["10"] = 22, ["12"] = 22, ["16"] = 20, ["18"] = 24, ["20"] = 19, ["24"] = 35, ["26"] = 35, ["30"] = 22, ["36"] = 19, ["38"] = 19,
        ["64"] = 59, ["66"] = 27, ["94"] = 1, ["96"] = 0, ["98"] = 36,
    };

    /// <summary>
    /// BX theo thứ tự SIP 2.00: 23, 11, 09, 01, 99, 97, 93, 63, 35, 37, 17, 19, 25, 15, 29, 65 — 'Y' cho lệnh được xử lý thật.
    /// 37 (thu phí) trả "không nhận" vì trạm không thu tiền; 15 (đặt mượn) làm ở OPAC.
    /// </summary>
    public const string Supported = "YYYNYYYYYNYNNNYY";
}

public static class Sip2Tags
{
    public const string PatronId = "AA";
    public const string ItemId = "AB";
    public const string TerminalPassword = "AC";
    public const string PatronPassword = "AD";
    public const string PersonalName = "AE";
    public const string ScreenMessage = "AF";
    public const string PrintLine = "AG";
    public const string DueDate = "AH";
    public const string TitleId = "AJ";
    public const string LibraryName = "AM";
    public const string InstitutionId = "AO";
    public const string CurrentLocation = "AP";
    public const string PermanentLocation = "AQ";
    public const string HoldItems = "AS";
    public const string OverdueItems = "AT";
    public const string ChargedItems = "AU";
    public const string FineItems = "AV";
    public const string HomeAddress = "BD";
    public const string Email = "BE";
    public const string Phone = "BF";
    public const string CurrencyType = "BH";
    public const string ValidPatron = "BL";
    public const string ValidPatronPassword = "BM";
    public const string RenewedItems = "BM";
    public const string UnrenewedItems = "BN";
    public const string FeeAmount = "BV";
    public const string SupportedMessages = "BX";
    public const string LoginUserId = "CN";
    public const string LoginPassword = "CO";
    public const string LocationCode = "CP";
    public const string HoldQueueLength = "CF";
}

/// <summary>Ngày giờ SIP2 "YYYYMMDDZZZZHHMMSS" theo giờ Việt Nam (vùng giờ để trống = giờ địa phương của thư viện).</summary>
public static class Sip2Clock
{
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    public static string Format(DateTimeOffset at) => at.ToOffset(Vietnam).ToString("yyyyMMdd    HHmmss", CultureInfo.InvariantCulture);
}

/// <summary>Đọc/ghi thông điệp và checksum (tổng bù 2 của các byte đã mã hoá, 16 bit, 4 chữ số hex).</summary>
public static class Sip2Codec
{
    public static Sip2Message Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var text = raw.TrimEnd('\r', '\n');
        if (text.Length < 2) throw new FormatException("Thông điệp SIP2 quá ngắn.");
        char? sequence = null;
        var az = text.LastIndexOf("AZ", StringComparison.Ordinal);
        if (az >= 0 && az == text.Length - 6) text = text[..az];
        var ay = text.LastIndexOf("AY", StringComparison.Ordinal);
        if (ay >= 0 && ay == text.Length - 3)
        {
            sequence = text[ay + 2];
            text = text[..ay];
        }

        var command = text[..2];
        var fixedLength = Sip2Commands.FixedLength.GetValueOrDefault(command);
        var fixedPart = text.Length - 2 >= fixedLength ? text.Substring(2, fixedLength) : text[2..];
        var message = new Sip2Message(command, fixedPart) { Sequence = sequence };
        foreach (var token in text[(2 + fixedPart.Length)..].Split('|', StringSplitOptions.RemoveEmptyEntries))
            if (token.Length >= 2) message.Add(token[..2], token[2..]);
        return message;
    }

    /// <summary>Thông điệp có AZ ở cuối thì checksum phải khớp (tính trên byte đã nhận); không có AZ thì bỏ qua.</summary>
    public static bool ChecksumValid(ReadOnlySpan<byte> line)
    {
        while (line.Length > 0 && line[^1] is (byte)'\r' or (byte)'\n') line = line[..^1];
        if (line.Length < 6 || line[^6] != (byte)'A' || line[^5] != (byte)'Z') return true;
        var expected = Encoding.ASCII.GetString(line[^4..]);
        return string.Equals(Checksum(line[..^4]), expected, StringComparison.OrdinalIgnoreCase);
    }

    public static string Checksum(ReadOnlySpan<byte> data)
    {
        var sum = 0;
        foreach (var b in data) sum += b;
        return ((-sum) & 0xFFFF).ToString("X4", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Mã hoá thông điệp trả lời (kèm AY khi trạm gửi số thứ tự, luôn có AZ) và ký tự kết thúc CR. <paramref name="asciiOnly"/>: bỏ dấu
    /// tiếng Việt cho máy chỉ hiển thị ASCII; ngược lại UTF-8.
    /// </summary>
    public static byte[] Encode(Sip2Message message, bool asciiOnly)
    {
        ArgumentNullException.ThrowIfNull(message);
        var text = new StringBuilder(message.Command).Append(message.Fixed);
        foreach (var (tag, value) in message.Fields) text.Append(tag).Append(Clean(value)).Append('|');
        if (message.Sequence is { } seq) text.Append("AY").Append(seq);
        text.Append("AZ");
        var body = asciiOnly ? Ascii(text.ToString()) : text.ToString();
        var bytes = Encoding.UTF8.GetBytes(body);
        return [.. bytes, .. Encoding.ASCII.GetBytes(Checksum(bytes) + "\r")];
    }

    /// <summary>Bỏ dấu tiếng Việt (đ → d), ký tự ngoài ASCII in được thay bằng '?'.</summary>
    public static string Ascii(string text)
    {
        var decomposed = (text ?? "").Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(ch switch
            {
                'đ' => 'd',
                'Đ' => 'D',
                >= ' ' and < (char)127 => ch,
                _ => '?',
            });
        }
        return builder.ToString();
    }

    /// <summary>Thông điệp đã đọc, dựng lại để ghi nhật ký — mật khẩu trạm/bạn đọc (CO, AD) thay bằng ***.</summary>
    public static string Mask(Sip2Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var text = new StringBuilder(message.Command).Append(message.Fixed);
        foreach (var (tag, value) in message.Fields)
            text.Append(tag).Append(tag is Sip2Tags.LoginPassword or Sip2Tags.PatronPassword ? "***" : value).Append('|');
        return text.ToString();
    }

    /// <summary>Ký tự '|' và xuống dòng trong giá trị làm hỏng khung thông điệp.</summary>
    private static string Clean(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
}
