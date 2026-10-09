using System.Text.Json;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Services;

public class FaceRecognitionService(
    ELIBAPIDbContext db,
    IGeminiClient gemini,
    IMinioService minio,
    IConfiguration config,
    ILogger<FaceRecognitionService> logger) : IFaceRecognitionService
{
    private readonly int _batchSize = config.GetValue<int?>("FaceRecognition:BatchSize") ?? 15;
    private readonly double _confidenceThreshold = config.GetValue<double?>("FaceRecognition:ConfidenceThreshold") ?? 0.7;

    public async Task<bool> HasFacePhotoAsync(long readerId, CancellationToken ct = default) =>
        await db.Readers.AnyAsync(x => x.Id == readerId && x.IsDelete != 2 && x.Photo != null && x.Photo != "", ct)
        || await db.ReaderPhotos.AnyAsync(p => p.ReaderId == readerId && p.IsDelete != 2 && p.PhotoUrl != null && p.PhotoUrl != "", ct);

    public async Task<FaceMatchResult?> IdentifyReaderAsync(string capturedImageBase64, long? tenantId,
        IReadOnlyCollection<long>? candidateReaderIds = null, CancellationToken ct = default)
    {
        if (candidateReaderIds is { Count: 0 }) return null;
        var only = candidateReaderIds?.Distinct().ToList();

        // Ảnh bổ sung (ReaderPhoto) — 1 bạn đọc có thể có nhiều ảnh, tăng độ ổn định nhận diện khi đổi
        // kiểu tóc/đeo kính/góc chụp khác nhau so với ảnh chính Reader.Photo.
        var photoQuery = db.ReaderPhotos
            .Where(p => p.IsDelete != 2 && p.PhotoUrl != null && p.PhotoUrl != ""
                && (!tenantId.HasValue || p.TenantId == tenantId));
        if (only != null) photoQuery = photoQuery.Where(p => only.Contains(p.ReaderId));
        var extraPhotos = await photoQuery.ToListAsync(ct);
        var extraByReader = extraPhotos.GroupBy(p => p.ReaderId).ToDictionary(g => g.Key, g => g.Select(p => p.PhotoUrl!).ToList());
        var readerIdsWithExtra = extraByReader.Keys.ToList();

        var readerQuery = db.Readers
            .Where(x => x.IsDelete != 2 && (!tenantId.HasValue || x.TenantId == tenantId)
                && ((x.Photo != null && x.Photo != "") || readerIdsWithExtra.Contains(x.Id)));
        if (only != null) readerQuery = readerQuery.Where(x => only.Contains(x.Id));
        var candidates = await readerQuery.ToListAsync(ct);

        if (candidates.Count == 0) return null;

        using var http = new HttpClient();

        // 1 bạn đọc có thể chiếm nhiều entry (ảnh chính + mọi ảnh bổ sung) — thuật toán so khớp/chia
        // batch bên dưới hoạt động trên danh sách phẳng này, không quan tâm 1 Reader lặp lại bao nhiêu
        // lần, nên không cần sửa gì thêm ở phần đó.
        var withPhotoBytes = new List<(Core.Entities.Dbo.Reader Reader, string Base64)>();
        foreach (var reader in candidates)
        {
            var photoUrls = new List<string>();
            if (!string.IsNullOrEmpty(reader.Photo)) photoUrls.Add(reader.Photo);
            if (extraByReader.TryGetValue(reader.Id, out var extra)) photoUrls.AddRange(extra);

            foreach (var photoUrl in photoUrls)
            {
                try
                {
                    var url = ResolveImageUrl(photoUrl);
                    var bytes = await http.GetByteArrayAsync(url, ct);
                    withPhotoBytes.Add((reader, Convert.ToBase64String(bytes)));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "FaceRecognitionService: không tải được ảnh bạn đọc {ReaderId}", reader.Id);
                }
            }
        }

        // So khớp TOÀN BỘ ứng viên rồi mới chọn — không dừng ở lô đầu tiên đạt ngưỡng, vì 1 lô sớm có
        // thể chứa 1 người trông giống (đạt ngưỡng) trong khi người khớp thật sự nằm ở lô sau; phải lấy
        // đúng người có độ tin cậy CAO NHẤT trên toàn bộ ứng viên, không phải người đầu tiên đạt ngưỡng.
        FaceMatchResult? best = null;
        foreach (var batch in withPhotoBytes.Chunk(_batchSize))
        {
            var (matchIndex, confidence) = await IdentifyBatchAsync(capturedImageBase64, batch.Select(x => x.Base64).ToList(), ct);
            if (matchIndex.HasValue && matchIndex.Value >= 1 && matchIndex.Value <= batch.Length && confidence >= _confidenceThreshold)
            {
                var reader = batch[matchIndex.Value - 1].Reader;
                if (best == null || confidence > best.Confidence)
                    best = new FaceMatchResult(
                        reader.Id, reader.PublicId, reader.Cardno,
                        $"{reader.LastName} {reader.FirstName}".Trim(),
                        ResolveImageUrl(reader.Photo), confidence);
            }
        }

        return best;
    }

    private async Task<(int? MatchIndex, double Confidence)> IdentifyBatchAsync(string capturedBase64, List<string> candidateBase64, CancellationToken ct)
    {
        var prompt =
            $"Ảnh #0 là ảnh chụp trực tiếp từ camera. Ảnh #1 đến #{candidateBase64.Count} là ảnh chân dung " +
            "của các bạn đọc đã đăng ký, theo đúng thứ tự. So sánh ảnh #0 với từng ảnh còn lại, xác định " +
            "xem có đúng là cùng một người hay không. Chỉ trả lời khớp khi thực sự chắc chắn. Trả về DUY " +
            "NHẤT một chuỗi JSON không kèm giải thích, đúng định dạng: " +
            "{\"matchIndex\": <số thứ tự 1..N của ảnh khớp, hoặc null nếu không khớp>, \"confidence\": <độ tin cậy 0..1>}";

        var parts = new List<object> { new { text = prompt }, new { inline_data = new { mime_type = "image/jpeg", data = capturedBase64 } } };
        parts.AddRange(candidateBase64.Select(b64 => (object)new { inline_data = new { mime_type = "image/jpeg", data = b64 } }));

        try
        {
            var result = await gemini.GenerateAsync(new { contents = new[] { new { parts = parts.ToArray() } } }, ct);
            var text = result.ValueKind == JsonValueKind.Object && result.TryGetProperty("text", out var t) ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(text)) return (null, 0);

            text = text.Trim();
            if (text.StartsWith("```"))
                text = text.Trim('`').Replace("json", "", StringComparison.OrdinalIgnoreCase).Trim();

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var matchIndex = root.TryGetProperty("matchIndex", out var mi) && mi.ValueKind == JsonValueKind.Number ? mi.GetInt32() : (int?)null;
            var confidence = root.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetDouble() : 0;
            return (matchIndex, confidence);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FaceRecognitionService: không phân tích được kết quả Gemini");
            return (null, 0);
        }
    }

    private string? ResolveImageUrl(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{minio.PublicBaseUrl}/{value}";
}
