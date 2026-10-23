using System.Text.RegularExpressions;
using Elib.BuildingBlocks.Domain;

namespace Elib.Portal.Domain;

/// <summary>Album ảnh (LRC: PhotoAlbum).</summary>
public sealed class PhotoAlbum : TenantEntity, IHasStatus
{
    private PhotoAlbum() { }

    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public string? Image { get; private set; }
    public string? Language { get; private set; }
    public int SortOrder { get; private set; }
    public int Status { get; private set; } = IHasStatus.Active;

    public static PhotoAlbum Create(string? name, string? description, string? image, string? language, int sortOrder, int status, string mediaPublicPrefix, long tenantId)
    {
        var album = new PhotoAlbum();
        album.Update(name, description, image, language, sortOrder, status, mediaPublicPrefix, tenantId);
        return album;
    }

    public void Update(string? name, string? description, string? image, string? language, int sortOrder, int status, string mediaPublicPrefix, long tenantId)
    {
        Name = TextRules.Required(name, 250, "NAME_INVALID", "Tên album");
        Description = TextRules.Optional(description, 1000, "DESCRIPTION_INVALID", "Mô tả");
        Image = PortalMedia.Image(image, mediaPublicPrefix, tenantId, "Ảnh đại diện album");
        Language = TextRules.Language(language);
        SortOrder = sortOrder;
        Status = StatusRules.Validate(status);
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);
}

/// <summary>Ảnh trong album (LRC: Photo).</summary>
public sealed class Photo : TenantEntity, IHasStatus
{
    private Photo() { }

    public long PhotoAlbumId { get; private set; }
    public string Name { get; private set; } = "";
    public string? Brief { get; private set; }
    public string Image { get; private set; } = "";
    public string? Link { get; private set; }
    public int SortOrder { get; private set; }
    public int Status { get; private set; } = IHasStatus.Active;

    public static Photo Create(long albumId, string? name, string? brief, string? image, string? link, int sortOrder, int status, string mediaPublicPrefix, long tenantId)
    {
        var photo = new Photo { PhotoAlbumId = albumId };
        photo.Update(name, brief, image, link, sortOrder, status, mediaPublicPrefix, tenantId);
        return photo;
    }

    public void Update(string? name, string? brief, string? image, string? link, int sortOrder, int status, string mediaPublicPrefix, long tenantId)
    {
        Name = TextRules.Required(name, 250, "NAME_INVALID", "Tên ảnh");
        Brief = TextRules.Optional(brief, 1000, "BRIEF_INVALID", "Mô tả");
        Image = PortalMedia.Image(image, mediaPublicPrefix, tenantId, "Ảnh") ?? throw new BusinessRuleException("MEDIA_URL_INVALID", "Ảnh bắt buộc.");
        Link = SafeLink.Validate(link, "Liên kết");
        SortOrder = sortOrder;
        Status = StatusRules.Validate(status);
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);
}

/// <summary>
/// Video (LRC: Video) — chỉ nhúng từ YouTube/Vimeo qua <see cref="VideoEmbed"/>: lưu nhà cung cấp + mã video đã kiểm, OPAC dựng iframe từ mã đó
/// (không bao giờ nhúng địa chỉ tuỳ ý).
/// </summary>
public sealed class Video : TenantEntity, IHasStatus
{
    private Video() { }

    public string Name { get; private set; } = "";
    public string? Description { get; private set; }

    /// <summary>youtube | vimeo</summary>
    public string Provider { get; private set; } = "";

    public string VideoId { get; private set; } = "";
    public string? Images { get; private set; }
    public string? Language { get; private set; }
    public int SortOrder { get; private set; }
    public int Status { get; private set; } = IHasStatus.Active;

    public static Video Create(string? name, string? description, string? link, string? images, string? language, int sortOrder, int status, string mediaPublicPrefix, long tenantId)
    {
        var video = new Video();
        video.Update(name, description, link, images, language, sortOrder, status, mediaPublicPrefix, tenantId);
        return video;
    }

    public void Update(string? name, string? description, string? link, string? images, string? language, int sortOrder, int status, string mediaPublicPrefix, long tenantId)
    {
        Name = TextRules.Required(name, 250, "NAME_INVALID", "Tên video");
        Description = TextRules.Optional(description, 1000, "DESCRIPTION_INVALID", "Mô tả");
        (Provider, VideoId) = VideoEmbed.Parse(link);
        Images = PortalMedia.Image(images, mediaPublicPrefix, tenantId, "Ảnh đại diện video");
        Language = TextRules.Language(language);
        SortOrder = sortOrder;
        Status = StatusRules.Validate(status);
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);

    /// <summary>Địa chỉ nhúng (iframe) dựng từ mã đã kiểm — dùng nguồn không cookie của YouTube.</summary>
    public string EmbedUrl => VideoEmbed.EmbedUrl(Provider, VideoId);
}

/// <summary>Nhận dạng video YouTube/Vimeo từ địa chỉ người dùng dán; mã video chỉ gồm ký tự an toàn.</summary>
public static partial class VideoEmbed
{
    public static (string Provider, string VideoId) Parse(string? link)
    {
        var v = link?.Trim() ?? "";
        if (v.Length is 0 or > 300 || !Uri.TryCreate(v, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new BusinessRuleException("VIDEO_LINK_INVALID", "Chỉ nhận địa chỉ video YouTube hoặc Vimeo.");
        var host = uri.Host.ToLowerInvariant();
        string? id = null;
        if (host is "youtu.be") id = uri.AbsolutePath.Trim('/');
        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com")
            id = uri.AbsolutePath.StartsWith("/embed/", StringComparison.Ordinal) ? uri.AbsolutePath[7..].Trim('/') : System.Web.HttpUtility.ParseQueryString(uri.Query)["v"];
        if (id is not null && YouTubeId().IsMatch(id)) return ("youtube", id);
        if (host is "vimeo.com" or "www.vimeo.com" && VimeoId().IsMatch(uri.AbsolutePath.Trim('/'))) return ("vimeo", uri.AbsolutePath.Trim('/'));
        throw new BusinessRuleException("VIDEO_LINK_INVALID", "Chỉ nhận địa chỉ video YouTube hoặc Vimeo.");
    }

    public static string EmbedUrl(string provider, string videoId) => provider == "vimeo"
        ? $"https://player.vimeo.com/video/{videoId}"
        : $"https://www.youtube-nocookie.com/embed/{videoId}";

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex YouTubeId();

    [GeneratedRegex("^[0-9]{5,12}$")]
    private static partial Regex VimeoId();
}
