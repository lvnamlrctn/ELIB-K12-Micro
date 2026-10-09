using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Cơ chế xem trước → xác nhận cho các thao tác ghi hàng loạt của tác vụ nền (Đợt 10 — port từ
/// <c>ReaderMutationGuard</c> của ELIB-LRC, đổi tên trung lập vì có thể tái dùng cho kind khác sau này;
/// v1 chỉ implement cho <see cref="Reader"/>). Ký HMAC-SHA256 toàn bộ tập thay đổi (before/after) +
/// request gốc; xác nhận phải tính lại đúng chữ ký từ dữ liệu HIỆN TẠI mới cho ghi — dữ liệu bị ai đó sửa
/// giữa lúc xem trước và lúc xác nhận sẽ làm lệch chữ ký, bị chặn với thông báo rõ ràng.
///
/// Khác ELIB-LRC: LRC xác thực lại theo TỪNG chunk ngay tại thời điểm ghi thật. ELIB v1 xác thực 1 LẦN
/// tại thời điểm xác nhận (AdminTaskService.Enqueue, nhánh confirm) rồi mới enqueue tác vụ ghi thật — đơn
/// giản hơn, chấp nhận 1 khe hở lý thuyết rất hẹp giữa lúc xác nhận và lúc worker thực thi (vài giây tới
/// vài phút, do worker poll 3 giây/lần) thay vì chặn tuyệt đối tới tận từng chunk. Có thể siết chặt thêm
/// (xác thực lại theo từng chunk) ở đợt sau nếu thấy cần thiết trong thực tế vận hành.</summary>
public static class AdminMutationGuard
{
    private static byte[] _key = RandomNumberGenerator.GetBytes(32);
    private static readonly JsonSerializerOptions Json = new();

    // Chỉ các field nghiệp vụ có ý nghĩa hiển thị đối chiếu — cố ý dùng allowlist (không phải blocklist)
    // để không bao giờ vô tình lộ field nhạy cảm (Password, ...) hay field hệ thống (UpdatedRowDate,
    // CreatedRowBy, ...) vào bảng diff hiển thị cho người duyệt.
    private static readonly string[] DiffFields =
    [
        nameof(Reader.LastName), nameof(Reader.FirstName), nameof(Reader.CitizenId), nameof(Reader.Sex),
        nameof(Reader.BirthDate), nameof(Reader.Email), nameof(Reader.Phone), nameof(Reader.Address),
        nameof(Reader.IssueDate), nameof(Reader.ExpireDate), nameof(Reader.ClassId), nameof(Reader.CourseId),
        nameof(Reader.OrgId), nameof(Reader.ReaderTypeId),
    ];

    /// <summary>Gọi 1 lần lúc khởi động (Program.cs), cùng nguồn secret với AdminTaskCrypto nhưng salt
    /// khác — 2 khoá dẫn xuất không thể hoán đổi cho nhau dù cùng 1 secret gốc.</summary>
    public static void ConfigureKey(string secret)
        => _key = SHA256.HashData(Encoding.UTF8.GetBytes("admin-preview-v1:" + secret));

    /// <summary>Định dạng giá trị field theo văn hoá BẤT BIẾN (InvariantCulture) — bắt buộc, vì hàm này
    /// chạy cả trong luồng xử lý request HTTP (văn hoá theo Accept-Language/middleware) lẫn trong tác vụ
    /// nền (BackgroundService, không có request nên văn hoá mặc định khác) — dùng <c>ToString()</c> trần
    /// (phụ thuộc CurrentCulture của luồng gọi) khiến 2 lần tính cùng 1 giá trị (lúc xem trước trong worker,
    /// lúc xác nhận lại trong request) ra 2 chuỗi khác nhau (vd DateTime: "21/09/2026" vs "9/21/2026"),
    /// làm chữ ký HMAC lệch dù dữ liệu không hề đổi. Lỗi này đã tự bắt được qua kiểm thử trực tiếp.</summary>
    private static string? FormatValue(object? value) => value switch
    {
        null => null,
        DateTime dt => dt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    /// <summary>Đọc ChangeTracker sau khi đã stage Added/Modified &lt;see cref="Reader"/&gt; (trước
    /// SaveChanges) để tính diff before/after theo <see cref="DiffFields"/>.</summary>
    public static List<ReaderChangePreview> BuildChanges(ELIBAPIDbContext db)
    {
        var changes = new List<ReaderChangePreview>();
        foreach (var entry in db.ChangeTracker.Entries<Reader>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;

            var fields = new List<ReaderFieldChange>();
            foreach (var name in DiffFields)
            {
                var prop = entry.Property(name);
                var before = entry.State == EntityState.Added ? null : FormatValue(prop.OriginalValue);
                var after = FormatValue(prop.CurrentValue);
                if (!string.Equals(before, after, StringComparison.Ordinal))
                    fields.Add(new ReaderFieldChange { Field = name, Before = before, After = after });
            }
            if (entry.State == EntityState.Modified && fields.Count == 0) continue;

            changes.Add(new ReaderChangePreview
            {
                Cardno = entry.Property(nameof(Reader.Cardno)).CurrentValue as string,
                IsNew = entry.State == EntityState.Added,
                Fields = fields,
            });
        }
        return changes.OrderBy(c => c.Cardno, StringComparer.Ordinal).ToList();
    }

    public static string Sign(object requestSummary, List<ReaderChangePreview> changes, long expiresUnix, Guid operationId)
    {
        var toSign = new { payload = requestSummary, changes, expires = expiresUnix, operationId };
        var hmac = Convert.ToHexString(HMACSHA256.HashData(_key, JsonSerializer.SerializeToUtf8Bytes(toSign, Json)));
        return $"{expiresUnix}:{operationId:N}:{hmac}";
    }

    public static (long Expires, Guid OperationId) ParseToken(string token)
    {
        var parts = token.Split(':', 3);
        if (parts.Length != 3 || !long.TryParse(parts[0], out var expires) || !Guid.TryParseExact(parts[1], "N", out var opId))
            throw new InvalidOperationException("Token xem trước không hợp lệ.");
        return (expires, opId);
    }

    /// <summary>Ném <see cref="InvalidOperationException"/> nếu hạn đã qua (trừ khi <paramref name="skipExpiry"/>)
    /// hoặc nội dung không khớp chữ ký đã ký lúc xem trước.</summary>
    public static void Verify(object requestSummary, List<ReaderChangePreview> changes, string token, bool skipExpiry)
    {
        var (expires, operationId) = ParseToken(token);
        if (!skipExpiry && DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expires)
            throw new InvalidOperationException("Phiên xem trước đã hết hạn. Vui lòng xem trước lại.");

        var expected = Sign(requestSummary, changes, expires, operationId);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(token);
        if (expectedBytes.Length != actualBytes.Length || !CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
            throw new InvalidOperationException("Dữ liệu hoặc điều kiện đã thay đổi. Vui lòng xem trước lại.");
    }
}
