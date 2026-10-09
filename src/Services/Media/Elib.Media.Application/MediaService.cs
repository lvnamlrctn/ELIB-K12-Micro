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

/// <summary>
/// Upload hai bước qua URL ký sẵn: request (cấp URL PUT vào bucket riêng tư) → trình duyệt PUT → complete (kiểm tra dung lượng
/// và magic bytes; file công khai được chép sang bucket công khai với Content-Type thật). Nội dung chưa kiểm tra không bao giờ
/// nằm ở bucket công khai — không ai dùng được URL upload để đưa HTML/script lên domain của hệ thống.
/// </summary>
public sealed class MediaService(IMediaDb db, IObjectStorage storage, ITenantContext tenant, ICurrentActor actor, TimeProvider clock, IOptions<MediaOptions> options)
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
