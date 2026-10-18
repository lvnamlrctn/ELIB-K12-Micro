using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Storage;
using Elib.BuildingBlocks.Tenancy;
using Elib.Media.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elib.Media.Application;

public interface IMediaDb
{
    DbSet<MediaFile> Files { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>Bucket đọc công khai (logo, ảnh bìa, ảnh CMS — docs 04 §6).</summary>
    public string PublicBucket { get; set; } = "media-public";

    /// <summary>Bucket riêng tư: file chờ kiểm tra và file chỉ tải qua URL ký có hạn.</summary>
    public string PrivateBucket { get; set; } = "media-private";

    public int UploadUrlMinutes { get; set; } = 10;
    public int DownloadUrlMinutes { get; set; } = 5;

    /// <summary>File chờ upload quá số giờ này bị dọn (object + bản ghi).</summary>
    public int PendingRetentionHours { get; set; } = 24;
}

public sealed record UploadRequest(string Purpose, string FileName, string ContentType, long Size);

/// <summary>Quản trị nền tảng upload hộ đơn vị (logo đơn vị).</summary>
public sealed record SystemUploadRequest(long TenantId, string Purpose, string FileName, string ContentType, long Size);

/// <summary>uploadUrl: trình duyệt PUT nội dung file vào đây (không kèm Authorization), rồi gọi complete.</summary>
public sealed record UploadTicketDto(Guid FileId, string UploadUrl, DateTimeOffset ExpiresAt, long MaxBytes);

/// <summary>url: đường dẫn cố định nếu file công khai; null với file riêng tư (lấy qua download).</summary>
public sealed record MediaFileDto(Guid Id, string Purpose, string FileName, string ContentType, long Size, MediaFileStatus Status, string? Url);

public sealed record DownloadDto(string Url, DateTimeOffset? ExpiresAt);

public sealed record FileContent(byte[] Bytes, string ContentType);

public sealed record FileUrlsRequest(IReadOnlyList<Guid>? Ids);

public sealed record FileUrlDto(Guid Id, string Url, DateTimeOffset? ExpiresAt);

/// <summary>Lấy file từ URL ngoài về kho (ảnh bìa tra theo ISBN…).</summary>
public sealed record ImportRequest(string Purpose, string Url);

/// <summary>Tải file từ Internet (Infrastructure): chỉ địa chỉ công khai, dừng khi vượt <c>maxBytes</c>. Lỗi → BusinessRuleException có câu báo.</summary>
public interface IRemoteFileFetcher
{
    Task<byte[]> FetchAsync(Uri url, long maxBytes, CancellationToken ct);
}

/// <summary>
/// Upload hai bước qua URL ký sẵn: request (cấp URL PUT vào bucket riêng tư) → trình duyệt PUT → complete (kiểm tra dung lượng
/// và magic bytes; file công khai được chép sang bucket công khai với Content-Type thật). Nội dung chưa kiểm tra không bao giờ
/// nằm ở bucket công khai — không ai dùng được URL upload để đưa HTML/script lên domain của hệ thống.
/// </summary>
public sealed class MediaService(IMediaDb db, IObjectStorage storage, ITenantContext tenant, ICurrentActor actor, TimeProvider clock, IOptions<MediaOptions> options,
    IRemoteFileFetcher fetcher)
{
    private readonly MediaOptions _options = options.Value;

    public async Task<UploadTicketDto> RequestUploadAsync(UploadRequest request, CancellationToken ct)
    {
        if (actor.Kind != "staff")
            throw new BusinessRuleException("MEDIA_STAFF_ONLY", "Chỉ cán bộ thư viện được upload file.", 403);
        var purpose = MediaPurpose.Get(request.Purpose);
        if (purpose.SystemOnly)
            throw new BusinessRuleException("MEDIA_SYSTEM_ONLY", $"{purpose.Name} do quản trị nền tảng cập nhật.", 403);
        return await CreateTicketAsync(tenant.RequireTenantId(), purpose, request.FileName, request.ContentType, request.Size, ct);
    }

    /// <summary>Gọi trong ngữ cảnh hệ thống (endpoint [RequireSystemContext]): chuyển sang đơn vị đích rồi làm như đơn vị tự upload.</summary>
    public async Task<UploadTicketDto> RequestSystemUploadAsync(SystemUploadRequest request, CancellationToken ct)
    {
        if (request.TenantId <= 0) throw new BusinessRuleException("MEDIA_TENANT_REQUIRED", "Chưa chọn đơn vị.");
        var purpose = MediaPurpose.Get(request.Purpose);
        using var _ = tenant.Use(request.TenantId);
        return await CreateTicketAsync(request.TenantId, purpose, request.FileName, request.ContentType, request.Size, ct);
    }

    public async Task<MediaFileDto> CompleteAsync(Guid fileId, CancellationToken ct)
    {
        var file = await FindAsync(fileId, ct);
        if (file.Status == MediaFileStatus.Ready) return ToDto(file); // gọi lại (mạng chập chờn) → trả kết quả cũ
        if (file.Status == MediaFileStatus.Rejected)
            throw new BusinessRuleException("MEDIA_REJECTED", "File đã bị từ chối, vui lòng upload lại.");

        var purpose = MediaPurpose.Get(file.Purpose);
        var stat = await storage.StatAsync(file.Bucket, file.ObjectKey, ct)
            ?? throw new BusinessRuleException("MEDIA_NOT_UPLOADED", "Chưa nhận được nội dung file, vui lòng upload lại.");
        if (stat.Size <= 0 || stat.Size > purpose.MaxBytes)
            await RejectAsync(file, "MEDIA_TOO_LARGE", $"{purpose.Name} tối đa {purpose.MaxSizeText}.", ct);

        // File công khai đọc trọn (đã giới hạn dung lượng) để chép sang bucket công khai; file riêng tư chỉ cần phần đầu.
        var content = await storage.ReadAsync(file.Bucket, file.ObjectKey, purpose.Public ? stat.Size : ContentSniffer.HeaderLength, ct);
        var detected = ContentSniffer.Detect(content);
        if (detected is null || !purpose.AllowedTypes.Contains(detected))
            await RejectAsync(file, "MEDIA_TYPE_INVALID", $"Nội dung file không phải {MediaFile.AllowedText(purpose)} hợp lệ.", ct);

        if (purpose.Public)
        {
            var publicKey = file.KeyFor(detected!, clock.GetUtcNow());
            await storage.PutAsync(_options.PublicBucket, publicKey, content, detected!, ct);
            await storage.DeleteAsync(file.Bucket, file.ObjectKey, ct);
            file.MarkReady(_options.PublicBucket, publicKey, detected!, stat.Size);
        }
        else
        {
            file.MarkReady(file.Bucket, file.ObjectKey, detected!, stat.Size);
        }
        await db.SaveChangesAsync(ct);
        return ToDto(file);
    }

    /// <summary>
    /// Lấy ảnh công khai từ URL ngoài về bucket công khai (chỉ mục đích công khai do đơn vị tự cập nhật — ảnh bìa). Trang của hệ thống chỉ hiện
    /// ảnh trong kho của mình (CSP img-src 'self'): trình duyệt bạn đọc không gọi ra Google/Open Library, ảnh không mất khi nguồn đổi đường dẫn.
    /// Nội dung kiểm tra như upload (dung lượng, magic bytes).
    /// </summary>
    public async Task<MediaFileDto> ImportAsync(ImportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (actor.Kind != "staff")
            throw new BusinessRuleException("MEDIA_STAFF_ONLY", "Chỉ cán bộ thư viện được upload file.", 403);
        var purpose = MediaPurpose.Get(request.Purpose);
        if (!purpose.Public || purpose.SystemOnly)
            throw new BusinessRuleException("MEDIA_IMPORT_NOT_ALLOWED", $"{purpose.Name} không lấy từ URL được — upload file.");
        if (!Uri.TryCreate((request.Url ?? "").Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) || !string.IsNullOrEmpty(uri.UserInfo))
            throw new BusinessRuleException("MEDIA_URL_INVALID", "Đường dẫn ảnh phải là URL http(s) đầy đủ.");

        var content = await fetcher.FetchAsync(uri, purpose.MaxBytes, ct);
        var detected = ContentSniffer.Detect(content);
        if (detected is null || !purpose.AllowedTypes.Contains(detected))
            throw new BusinessRuleException("MEDIA_TYPE_INVALID", $"Nội dung tải về không phải {MediaFile.AllowedText(purpose)} hợp lệ.");

        var now = clock.GetUtcNow();
        var name = Path.GetFileName(uri.AbsolutePath);
        var file = MediaFile.Create(tenant.RequireTenantId(), purpose, name.Length is > 0 and <= 255 ? name : "anh", detected, content.Length,
            _options.PublicBucket, now);
        await storage.PutAsync(_options.PublicBucket, file.ObjectKey, content, detected, ct);
        file.MarkReady(_options.PublicBucket, file.ObjectKey, detected, content.Length);
        db.Files.Add(file);
        await db.SaveChangesAsync(ct);
        return ToDto(file);
    }

    public async Task<MediaFileDto> CompleteSystemAsync(long tenantId, Guid fileId, CancellationToken ct)
    {
        using var _ = tenant.Use(tenantId);
        return await CompleteAsync(fileId, ct);
    }

    public async Task<MediaFileDto> GetAsync(Guid fileId, CancellationToken ct) => ToDto(await FindAsync(fileId, ct));

    /// <summary>URL tải: cố định với file công khai, ký có hạn với file riêng tư.</summary>
    public async Task<DownloadDto> DownloadAsync(Guid fileId, CancellationToken ct)
    {
        var file = await FindAsync(fileId, ct);
        if (file.Status != MediaFileStatus.Ready) throw new NotFoundException("file", fileId);
        if (file.IsPublic) return new DownloadDto(storage.PublicUrl(file.Bucket, file.ObjectKey), null);
        var expiry = TimeSpan.FromMinutes(_options.DownloadUrlMinutes);
        return new DownloadDto(await storage.PresignGetAsync(file.Bucket, file.ObjectKey, expiry), clock.GetUtcNow() + expiry);
    }

    /// <summary>
    /// Nội dung ảnh bạn đọc cho service khác (nhận diện khuôn mặt, service token) — chỉ file mục đích reader-photo đã xong, của đơn vị hiện tại.
    /// Nội dung không đi qua gateway; service gọi không có quyền đọc các loại file khác.
    /// </summary>
    public async Task<FileContent> ReadReaderPhotoAsync(Guid fileId, CancellationToken ct)
    {
        var file = await FindAsync(fileId, ct);
        if (file.Status != MediaFileStatus.Ready || file.Purpose != MediaPurpose.ReaderPhoto.Code) throw new NotFoundException("file", fileId);
        var bytes = await storage.ReadAsync(file.Bucket, file.ObjectKey, MediaPurpose.ReaderPhoto.MaxBytes, ct);
        return new FileContent(bytes, file.ContentType);
    }

    /// <summary>
    /// URL có hạn của file tài liệu số cho digital (service token) — CHỈ file mục đích ebook-file đã xong, của đơn vị hiện tại.
    /// Giới hạn theo mục đích để digital không xin được URL của ảnh thẻ/tệp đính kèm thuộc service khác. digital tự kiểm quyền mượn
    /// trước khi gọi — ở đây chỉ kiểm file đúng loại và đã sẵn sàng.
    /// </summary>
    public async Task<DownloadDto> DownloadEbookFileAsync(Guid fileId, CancellationToken ct)
    {
        var file = await FindAsync(fileId, ct);
        if (file.Status != MediaFileStatus.Ready || file.Purpose != MediaPurpose.EbookFile.Code) throw new NotFoundException("file", fileId);
        var expiry = TimeSpan.FromMinutes(_options.DownloadUrlMinutes);
        return new DownloadDto(await storage.PresignGetAsync(file.Bucket, file.ObjectKey, expiry), clock.GetUtcNow() + expiry);
    }

    public const int MaxUrlsPerCall = 100;

    /// <summary>
    /// Link xem nhiều file một lần (ảnh nhỏ trong danh sách bạn đọc, in thẻ): file không có, chưa xong, hoặc không thuộc đơn vị thì bỏ qua
    /// (không báo lỗi cả lượt). Link ký có hạn như <see cref="DownloadAsync"/>.
    /// </summary>
    public async Task<IReadOnlyList<FileUrlDto>> UrlsAsync(FileUrlsRequest request, CancellationToken ct)
    {
        var ids = (request.Ids ?? []).Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count > MaxUrlsPerCall)
            throw new BusinessRuleException("MEDIA_URLS_TOO_MANY", $"Mỗi lần lấy tối đa {MaxUrlsPerCall} link.");
        var files = await db.Files.AsNoTracking().Where(f => ids.Contains(f.PublicId) && f.Status == MediaFileStatus.Ready).ToListAsync(ct);
        var expiry = TimeSpan.FromMinutes(_options.DownloadUrlMinutes);
        var result = new List<FileUrlDto>(files.Count);
        foreach (var file in files)
        {
            result.Add(file.IsPublic
                ? new FileUrlDto(file.PublicId, storage.PublicUrl(file.Bucket, file.ObjectKey), null)
                : new FileUrlDto(file.PublicId, await storage.PresignGetAsync(file.Bucket, file.ObjectKey, expiry), clock.GetUtcNow() + expiry));
        }
        return result;
    }

    private async Task<UploadTicketDto> CreateTicketAsync(long tenantId, MediaPurpose purpose, string fileName, string contentType, long size, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var file = MediaFile.Create(tenantId, purpose, fileName, contentType, size, _options.PrivateBucket, now);
        db.Files.Add(file);
        await db.SaveChangesAsync(ct);

        var expiry = TimeSpan.FromMinutes(_options.UploadUrlMinutes);
        var url = await storage.PresignPutAsync(file.Bucket, file.ObjectKey, expiry);
        return new UploadTicketDto(file.PublicId, url, now + expiry, purpose.MaxBytes);
    }

    private async Task<MediaFile> FindAsync(Guid fileId, CancellationToken ct) =>
        await db.Files.FirstOrDefaultAsync(f => f.PublicId == fileId, ct) ?? throw new NotFoundException("file", fileId);

    private async Task RejectAsync(MediaFile file, string code, string message, CancellationToken ct)
    {
        await storage.DeleteAsync(file.Bucket, file.ObjectKey, ct);
        file.Reject();
        await db.SaveChangesAsync(ct);
        throw new BusinessRuleException(code, message);
    }

    private MediaFileDto ToDto(MediaFile f) => new(
        f.PublicId, f.Purpose, f.FileName, f.ContentType, f.Size, f.Status,
        f.IsPublic && f.Status == MediaFileStatus.Ready ? storage.PublicUrl(f.Bucket, f.ObjectKey) : null);
}

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddMediaApplication(this IServiceCollection services)
    {
        services.AddScoped<MediaService>();
        return services;
    }
}
