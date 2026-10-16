using System.Globalization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Patron.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elib.Patron.Application;

/// <summary>
/// Cấu hình nhận diện khuôn mặt (<c>FaceRecognition:*</c>). Không có <see cref="ApiKey"/> thì tính năng tắt. <see cref="AllowedTenantIds"/>
/// rỗng = mọi đơn vị; có giá trị = chỉ các đơn vị đó (ảnh bạn đọc chỉ rời hệ thống khi đơn vị đã được bật).
/// </summary>
public sealed class FaceRecognitionOptions
{
    public const string Section = "FaceRecognition";

    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gemini-2.0-flash-lite";

    /// <summary>Số ảnh bạn đọc gửi kèm mỗi lượt gọi Gemini.</summary>
    public int BatchSize { get; set; } = 15;

    /// <summary>Độ tin cậy tối thiểu (0..1) để coi là khớp.</summary>
    public double ConfidenceThreshold { get; set; } = 0.7;

    /// <summary>Số lượt gọi Gemini chạy song song (mỗi lượt một lô ảnh).</summary>
    public int MaxParallel { get; set; } = 4;

    /// <summary>
    /// Tổng dung lượng ảnh bạn đọc (byte gốc) trong một lượt gọi Gemini. Gemini giới hạn yêu cầu có ảnh nhúng khoảng 20 MB (base64 lớn hơn ~33%),
    /// nên 8 MB + ảnh camera luôn nằm dưới trần; ảnh tải lên chưa thu nhỏ có thể tới 2 MB mỗi ảnh.
    /// </summary>
    public int MaxBatchBytes { get; set; } = 8_000_000;

    /// <summary>Ảnh camera tối đa (byte, sau khi giải base64).</summary>
    public int MaxImageBytes { get; set; } = 1_500_000;

    public List<long> AllowedTenantIds { get; set; } = [];

    public bool Configured => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>Kết quả so khớp một lô: số thứ tự (1..n) của ảnh khớp nhất, null nếu không khớp ai.</summary>
public sealed record FaceBatchMatch(int? Index, double Confidence);

/// <summary>So ảnh camera với một lô ảnh bạn đọc (Gemini). Ném <see cref="FaceServiceException"/> khi dịch vụ ngoài lỗi.</summary>
public interface IFaceMatcher
{
    Task<FaceBatchMatch> MatchAsync(byte[] camera, IReadOnlyList<byte[]> candidates, CancellationToken ct);
}

/// <summary>Đọc nội dung ảnh bạn đọc từ media (service token).</summary>
public interface IReaderPhotoSource
{
    Task<byte[]?> ReadAsync(Guid fileId, CancellationToken ct);
}

public sealed class FaceServiceException(string message, Exception? inner = null) : Exception(message, inner);

public sealed record FaceStatus(bool Enabled);

public sealed record FaceIdentifyRequest(string? ImageBase64);

public sealed record FaceMatch(Guid ReaderPublicId, string CardNo, string FullName, double Confidence);

/// <summary>
/// Nhận diện bạn đọc từ ảnh camera (ELIB-LRC: FaceRecognitionService): ảnh thẻ và ảnh bổ sung của các bạn đọc trong đơn vị được chia lô,
/// mỗi lô một lượt gọi Gemini; chọn bạn đọc có độ tin cậy cao nhất trên TOÀN BỘ các lô (không dừng ở lô đầu đạt ngưỡng — người giống
/// ở lô sớm không được thắng người khớp thật ở lô sau). Ảnh camera chỉ nằm trong bộ nhớ của yêu cầu này, không lưu, không ghi log.
/// Chi phí: mỗi lần nhận diện tốn khoảng (số ảnh ÷ <see cref="FaceRecognitionOptions.BatchSize"/>) lượt gọi.
/// </summary>
public sealed partial class FaceRecognition(
    ICrudDbContext db, IFaceMatcher matcher, IReaderPhotoSource photos, ITenantContext tenant, IOptions<FaceRecognitionOptions> options,
    ILogger<FaceRecognition> logger)
{
    private readonly FaceRecognitionOptions _options = options.Value;

    public FaceStatus Status() => new(Enabled(tenant.TenantId));

    private bool Enabled(long? tenantId) =>
        _options.Configured && (_options.AllowedTenantIds.Count == 0 || (tenantId is { } id && _options.AllowedTenantIds.Contains(id)));

    public async Task<FaceMatch?> IdentifyAsync(string? imageBase64, CancellationToken ct)
    {
        var tenantId = tenant.RequireTenantId();
        if (!Enabled(tenantId))
            throw new BusinessRuleException("FACE_NOT_CONFIGURED", "Nhận diện khuôn mặt chưa được bật cho đơn vị này.", 409);
        var camera = Decode(imageBase64);

        // Ứng viên: bạn đọc của đơn vị có ảnh thẻ hoặc ảnh bổ sung (bộ lọc đơn vị của DbContext — không bao giờ lấy ảnh đơn vị khác).
        var readers = await db.Set<Reader>().AsNoTracking()
            .Where(r => r.PhotoId != null || db.Set<ReaderPhoto>().Any(p => p.ReaderId == r.Id))
            .Select(r => new { r.Id, r.PublicId, r.CardNo, r.LastName, r.FirstName, r.PhotoId })
            .ToListAsync(ct);
        if (readers.Count == 0) return null;
        var ids = readers.Select(r => r.Id).ToList();
        var extra = (await db.Set<ReaderPhoto>().AsNoTracking().Where(p => ids.Contains(p.ReaderId)).OrderBy(p => p.Id).Select(p => new { p.ReaderId, p.FileId }).ToListAsync(ct))
            .ToLookup(p => p.ReaderId, p => p.FileId);

        // Danh sách phẳng (bạn đọc, file ảnh): một bạn đọc chiếm nhiều dòng — ảnh thẻ rồi tới ảnh bổ sung.
        var entries = readers.SelectMany(r => (r.PhotoId is { } main ? [main] : Array.Empty<Guid>()).Concat(extra[r.Id]).Select(file => (Reader: r, File: file))).ToList();
        using var gate = new SemaphoreSlim(Math.Max(1, _options.MaxParallel));
        var loaded = await Task.WhenAll(entries.Select(async e =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var bytes = await photos.ReadAsync(e.File, ct);
                return bytes is { Length: > 0 } ? (e.Reader, Bytes: bytes, Failed: false) : (e.Reader, Bytes: (byte[]?)null, Failed: false);
            }
            catch (FaceServiceException ex)
            {
                LogPhotoFailed(ex, e.Reader.Id);
                return (e.Reader, Bytes: (byte[]?)null, Failed: true);
            }
            finally
            {
                gate.Release();
            }
        }));
        // Mọi lần đọc đều lỗi (token dịch vụ sai, media sập…) = báo lỗi, không để hiện "không nhận ra bạn đọc nào" mãi.
        if (loaded.Length > 0 && loaded.All(x => x.Failed))
            throw new BusinessRuleException("FACE_UNAVAILABLE", "Không đọc được ảnh bạn đọc từ kho ảnh — thử lại sau hoặc quét thẻ.", 503);
        var usable = loaded.Where(x => x.Bytes is not null).Select(x => (x.Reader, Bytes: x.Bytes!)).ToList();
        if (usable.Count == 0) return null;

        var size = Math.Clamp(_options.BatchSize, 1, 50);
        var batches = Batch(usable, u => u.Bytes.Length, size, Math.Max(100_000, _options.MaxBatchBytes));
        var results = await Task.WhenAll(batches.Select(async batch =>
        {
            await gate.WaitAsync(ct);
            try
            {
                return (Batch: batch, Match: await matcher.MatchAsync(camera, batch.Select(b => b.Bytes).ToList(), ct), Failed: false);
            }
            catch (FaceServiceException ex)
            {
                LogBatchFailed(ex);
                return (Batch: batch, Match: new FaceBatchMatch(null, 0), Failed: true);
            }
            finally
            {
                gate.Release();
            }
        }));

        // Mọi lô đều lỗi = dịch vụ ngoài không dùng được — báo lỗi thay vì "không nhận ra" gây hiểu nhầm.
        if (results.All(r => r.Failed))
            throw new BusinessRuleException("FACE_UNAVAILABLE", "Dịch vụ nhận diện khuôn mặt đang không dùng được — thử lại sau hoặc quét thẻ.", 503);
        var best = results
            .Where(r => r.Match.Index is >= 1 && r.Match.Index <= r.Batch.Length && r.Match.Confidence >= _options.ConfidenceThreshold)
            .OrderByDescending(r => r.Match.Confidence).FirstOrDefault();
        if (best.Batch is null) return null;
        var hit = best.Batch[best.Match.Index!.Value - 1].Reader;
        var name = string.IsNullOrWhiteSpace(hit.LastName) ? hit.FirstName : $"{hit.LastName} {hit.FirstName}";
        LogMatched(tenantId, hit.Id, best.Match.Confidence);
        return new FaceMatch(hit.PublicId, hit.CardNo, name, best.Match.Confidence);
    }

    /// <summary>Chia theo thứ tự thành lô tối đa <paramref name="maxCount"/> ảnh và <paramref name="maxBytes"/> byte (ảnh đơn lẻ lớn hơn trần vẫn nằm một mình một lô).</summary>
    internal static List<T[]> Batch<T>(IReadOnlyList<T> items, Func<T, int> sizeOf, int maxCount, int maxBytes)
    {
        var result = new List<T[]>();
        var current = new List<T>();
        long bytes = 0;
        foreach (var item in items)
        {
            var size = sizeOf(item);
            if (current.Count > 0 && (current.Count >= maxCount || bytes + size > maxBytes))
            {
                result.Add([.. current]);
                current.Clear();
                bytes = 0;
            }
            current.Add(item);
            bytes += size;
        }
        if (current.Count > 0) result.Add([.. current]);
        return result;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Không tải được ảnh bạn đọc {ReaderId} để nhận diện")]
    private partial void LogPhotoFailed(Exception ex, long readerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Một lô nhận diện khuôn mặt lỗi")]
    private partial void LogBatchFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Nhận diện khuôn mặt: đơn vị {TenantId} khớp bạn đọc {ReaderId}, độ tin cậy {Confidence:0.00}")]
    private partial void LogMatched(long tenantId, long readerId, double confidence);

    /// <summary>Ảnh camera: base64 thuần hoặc data URL; phải là JPEG/PNG, không rỗng và không quá <see cref="FaceRecognitionOptions.MaxImageBytes"/>.</summary>
    private byte[] Decode(string? value)
    {
        var s = (value ?? "").Trim();
        if (s.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && s.IndexOf(',') is var comma and >= 0) s = s[(comma + 1)..];
        if (s.Length == 0) throw new BusinessRuleException("FACE_IMAGE_REQUIRED", "Thiếu ảnh chụp.");
        if (s.Length > _options.MaxImageBytes * 4L / 3 + 8) throw new BusinessRuleException("FACE_IMAGE_TOO_BIG", "Ảnh chụp quá lớn.");
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(s);
        }
        catch (FormatException)
        {
            throw new BusinessRuleException("FACE_IMAGE_INVALID", "Ảnh chụp không hợp lệ.");
        }
        var jpeg = bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8;
        var png = bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
        if (!jpeg && !png) throw new BusinessRuleException("FACE_IMAGE_INVALID", "Ảnh chụp phải là JPEG hoặc PNG.");
        return bytes.Length <= _options.MaxImageBytes ? bytes : throw new BusinessRuleException("FACE_IMAGE_TOO_BIG", "Ảnh chụp quá lớn.");
    }
}
