using System.Globalization;
using Elib.BuildingBlocks.Domain;

namespace Elib.Media.Domain;

public enum MediaFileStatus
{
    /// <summary>Đã cấp URL upload, chờ trình duyệt PUT xong rồi gọi complete.</summary>
    Pending = 0,

    /// <summary>Đã kiểm tra nội dung, dùng được.</summary>
    Ready = 1,

    /// <summary>Nội dung sai loại/quá cỡ — object đã bị xoá.</summary>
    Rejected = 2,
}

/// <summary>
/// Mục đích upload quyết định bucket, loại file và dung lượng được phép (docs 05 §3: giới hạn theo loại).
/// <see cref="SystemOnly"/>: chỉ quản trị nền tảng upload hộ đơn vị (logo đơn vị).
/// </summary>
public sealed record MediaPurpose(string Code, string Name, bool Public, IReadOnlyList<string> AllowedTypes, long MaxBytes, bool SystemOnly)
{
    private const long MB = 1024 * 1024;

    public static readonly MediaPurpose TenantLogo =
        new("tenant-logo", "Logo đơn vị", Public: true, ["image/png", "image/jpeg", "image/webp"], 2 * MB, SystemOnly: true);

    public static readonly MediaPurpose Attachment =
        new("attachment", "Tệp đính kèm", Public: false, ["image/png", "image/jpeg", "image/gif", "image/webp", "application/pdf"], 20 * MB, SystemOnly: false);

    /// <summary>Ảnh thẻ bạn đọc — phần lớn là học sinh nên để riêng tư, chỉ xem qua URL ký có hạn.</summary>
    public static readonly MediaPurpose ReaderPhoto =
        new("reader-photo", "Ảnh bạn đọc", Public: false, ["image/png", "image/jpeg", "image/webp"], 2 * MB, SystemOnly: false);

    /// <summary>Ảnh bìa tài liệu — hiện công khai trên OPAC (catalog giữ URL trong biểu ghi).</summary>
    public static readonly MediaPurpose BibCover =
        new("bib-cover", "Ảnh bìa", Public: true, ["image/png", "image/jpeg", "image/webp"], 2 * MB, SystemOnly: false);

    public static readonly IReadOnlyList<MediaPurpose> All = [TenantLogo, Attachment, ReaderPhoto, BibCover];

    public static MediaPurpose Get(string? code) =>
        All.FirstOrDefault(p => string.Equals(p.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new BusinessRuleException("MEDIA_PURPOSE_INVALID", $"Mục đích upload '{code}' không được hỗ trợ.");

    public string MaxSizeText => (MaxBytes / MB).ToString(CultureInfo.InvariantCulture) + " MB";
}

/// <summary>
/// Metadata một file trên MinIO. File nghiệp vụ (bìa sách, ảnh bạn đọc…) vẫn do service nghiệp vụ giữ tham chiếu;
/// media chỉ lo upload, kiểm tra nội dung và cấp URL (docs 02 §media).
/// </summary>
public sealed class MediaFile : Entity, ITenantOwned, IHasPublicId, IAuditable
{
    private MediaFile() { }

    public long TenantId { get; set; }
    public Guid PublicId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }

    public string Purpose { get; private set; } = "";
    public string FileName { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public long Size { get; private set; }
    public MediaFileStatus Status { get; private set; }
    public string Bucket { get; private set; } = "";
    public string ObjectKey { get; private set; } = "";
    public bool IsPublic { get; private set; }

    /// <summary>
    /// File chờ upload vào <paramref name="stagingBucket"/> (luôn riêng tư — file công khai chỉ được chép sang bucket công khai sau khi kiểm tra).
    /// Key: {tenantId}/{mục đích}/{yyyy}/{MM}/{publicId}{đuôi}.
    /// </summary>
    public static MediaFile Create(long tenantId, MediaPurpose purpose, string fileName, string contentType, long size, string stagingBucket, DateTimeOffset now)
    {
        var name = Path.GetFileName((fileName ?? "").Replace('\\', '/')).Trim();
        if (name.Length is 0 or > 255)
            throw new BusinessRuleException("MEDIA_FILE_NAME_INVALID", "Tên file bắt buộc, tối đa 255 ký tự.");
        var type = (contentType ?? "").Trim().ToLowerInvariant();
        if (!purpose.AllowedTypes.Contains(type))
            throw new BusinessRuleException("MEDIA_TYPE_INVALID", $"{purpose.Name} chỉ nhận: {AllowedText(purpose)}.");
        if (size <= 0 || size > purpose.MaxBytes)
            throw new BusinessRuleException("MEDIA_TOO_LARGE", $"{purpose.Name} tối đa {purpose.MaxSizeText}.");

        var file = new MediaFile
        {
            TenantId = tenantId,
            PublicId = Guid.CreateVersion7(),
            Purpose = purpose.Code,
            FileName = name,
            ContentType = type,
            Size = size,
            Status = MediaFileStatus.Pending,
            Bucket = stagingBucket,
            IsPublic = purpose.Public,
        };
        file.ObjectKey = file.KeyFor(type, now);
        return file;
    }

    /// <summary>Key cho nội dung loại <paramref name="contentType"/> (đuôi theo loại thật, không theo tên người dùng gửi).</summary>
    public string KeyFor(string contentType, DateTimeOffset now) =>
        string.Create(CultureInfo.InvariantCulture, $"{TenantId}/{Purpose}/{now:yyyy}/{now:MM}/{PublicId:N}{ExtensionOf(contentType)}");

    public void MarkReady(string bucket, string objectKey, string contentType, long size)
    {
        if (Status != MediaFileStatus.Pending) throw new ConflictException("MEDIA_NOT_PENDING", "File không ở trạng thái chờ upload.");
        Bucket = bucket;
        ObjectKey = objectKey;
        ContentType = contentType;
        Size = size;
        Status = MediaFileStatus.Ready;
    }

    public void Reject()
    {
        if (Status == MediaFileStatus.Pending) Status = MediaFileStatus.Rejected;
    }

    public static string AllowedText(MediaPurpose purpose) =>
        string.Join(", ", purpose.AllowedTypes.Select(t => ExtensionOf(t).TrimStart('.').ToUpperInvariant()));

    private static string ExtensionOf(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        "application/pdf" => ".pdf",
        _ => "",
    };
}
