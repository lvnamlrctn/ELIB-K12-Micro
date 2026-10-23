using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Portal.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elib.Portal.Application;

public sealed class BannerSearch : CrudSearch
{
    public string? Position { get; set; }
}

public sealed record BannerRequest(
    string Name, string Image, string? Position = null, string? Link = null, int? Width = null, int? Height = null, int SortOrder = 0, string? Language = null,
    DateOnly? ShowFrom = null, DateOnly? ShowTo = null, int Status = IHasStatus.Active);

public sealed record BannerDto(
    long Id, Guid PublicId, string Name, string Position, string Image, string? Link, int? Width, int? Height, int SortOrder, string? Language, DateOnly? ShowFrom,
    DateOnly? ShowTo, int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Banner (LRC: BannerController + PublicHomeBanner, quyền PORTAL_BANNERS).</summary>
public sealed class BannerResource(ICrudDbContext db, ITenantContext tenant, IOptions<PortalOptions> options)
    : CrudResource<BannerResource, Banner, BannerSearch, BannerRequest, BannerDto>(db)
{
    private readonly PortalOptions _options = options.Value;

    protected override string EntityName => "Banner";

    protected override string? Describe(Banner entity) => entity.Name;

    protected override Expression<Func<Banner, BannerDto>> Projection => x => new BannerDto(
        x.Id, x.PublicId, x.Name, x.Position, x.Image, x.Link, x.Width, x.Height, x.SortOrder, x.Language, x.ShowFrom, x.ShowTo, x.Status, x.CreatedAt, x.UpdatedAt);

    protected override Banner Create(BannerRequest r) => Banner.Create(ToFields(r), _options.MediaPublicPrefix, tenant.RequireTenantId());

    protected override void Update(Banner entity, BannerRequest r) => entity.Update(ToFields(r), _options.MediaPublicPrefix, tenant.RequireTenantId());

    protected override IQueryable<Banner> Filter(IQueryable<Banner> query, BannerSearch search)
    {
        if (!string.IsNullOrWhiteSpace(search.Position)) query = query.Where(x => x.Position == search.Position);
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<Banner> Order(IQueryable<Banner> query) => query.OrderBy(x => x.Position).ThenBy(x => x.SortOrder).ThenBy(x => x.Id);

    private static BannerFields ToFields(BannerRequest r) => new(r.Name, r.Image, r.Position, r.Link, r.Width, r.Height, r.SortOrder, r.Language, r.ShowFrom, r.ShowTo, r.Status);
}

public sealed record AdsGroupRequest(string Name, string? Type = null, int? Width = null, int? Height = null, string? Language = null, int Status = IHasStatus.Active);

public sealed record AdsGroupDto(
    long Id, Guid PublicId, string Name, string? Type, int? Width, int? Height, string? Language, int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Nhóm quảng cáo (LRC: AdsGroupController, quyền PORTAL_ADS).</summary>
public sealed class AdsGroupResource(ICrudDbContext db) : CrudResource<AdsGroupResource, AdsGroup, CrudSearch, AdsGroupRequest, AdsGroupDto>(db)
{
    protected override string EntityName => "Nhóm quảng cáo";

    protected override string? Describe(AdsGroup entity) => entity.Name;

    protected override Expression<Func<AdsGroup, AdsGroupDto>> Projection => x =>
        new AdsGroupDto(x.Id, x.PublicId, x.Name, x.Type, x.Width, x.Height, x.Language, x.Status, x.CreatedAt, x.UpdatedAt);

    protected override AdsGroup Create(AdsGroupRequest r) => AdsGroup.Create(r.Name, r.Type, r.Width, r.Height, r.Language, r.Status);

    protected override void Update(AdsGroup entity, AdsGroupRequest r) => entity.Update(r.Name, r.Type, r.Width, r.Height, r.Language, r.Status);

    protected override async Task EnsureDeletableAsync(AdsGroup entity, CancellationToken ct)
    {
        if (await Db.Set<Ads>().AnyAsync(a => a.AdsGroupId == entity.Id, ct)) throw new ConflictException("ADS_GROUP_HAS_ADS", $"Nhóm '{entity.Name}' còn quảng cáo.");
    }

    protected override IQueryable<AdsGroup> Filter(IQueryable<AdsGroup> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<AdsGroup> Order(IQueryable<AdsGroup> query) => query.OrderBy(x => x.Name);
}

public sealed class AdsSearch : CrudSearch
{
    public long? AdsGroupId { get; set; }
}

public sealed record AdsRequest(
    long AdsGroupId, string Name, string Image, string? Link = null, int? Width = null, int? Height = null, int SortOrder = 0, DateOnly? ShowFrom = null,
    DateOnly? ShowTo = null, int Status = IHasStatus.Active);

public sealed record AdsDto(
    long Id, Guid PublicId, long AdsGroupId, string Name, string Image, string? Link, int? Width, int? Height, int SortOrder, DateOnly? ShowFrom, DateOnly? ShowTo,
    int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, long Clicks = 0);

/// <summary>Quảng cáo (LRC: AdsController, quyền PORTAL_ADS).</summary>
public sealed class AdsResource(ICrudDbContext db, ITenantContext tenant, IOptions<PortalOptions> options) : CrudResource<AdsResource, Ads, AdsSearch, AdsRequest, AdsDto>(db)
{
    private readonly PortalOptions _options = options.Value;

    protected override string EntityName => "Quảng cáo";

    protected override string? Describe(Ads entity) => entity.Name;

    protected override Expression<Func<Ads, AdsDto>> Projection => x => new AdsDto(
        x.Id, x.PublicId, x.AdsGroupId, x.Name, x.Image, x.Link, x.Width, x.Height, x.SortOrder, x.ShowFrom, x.ShowTo, x.Status, x.CreatedAt, x.UpdatedAt, x.Clicks);

    protected override Ads Create(AdsRequest r) =>
        Ads.Create(r.AdsGroupId, r.Name, r.Image, r.Link, r.Width, r.Height, r.SortOrder, r.ShowFrom, r.ShowTo, r.Status, _options.MediaPublicPrefix, tenant.RequireTenantId());

    protected override void Update(Ads entity, AdsRequest r) =>
        entity.Update(r.Name, r.Image, r.Link, r.Width, r.Height, r.SortOrder, r.ShowFrom, r.ShowTo, r.Status, _options.MediaPublicPrefix, tenant.RequireTenantId());

    protected override async Task ValidateAsync(Ads entity, CancellationToken ct)
    {
        if (!await Db.Set<AdsGroup>().AnyAsync(g => g.Id == entity.AdsGroupId, ct)) throw new BusinessRuleException("ADS_GROUP_NOT_FOUND", "Nhóm quảng cáo đã chọn không còn.");
    }

    protected override IQueryable<Ads> Filter(IQueryable<Ads> query, AdsSearch search)
    {
        if (search.AdsGroupId is { } group) query = query.Where(x => x.AdsGroupId == group);
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<Ads> Order(IQueryable<Ads> query) => query.OrderBy(x => x.AdsGroupId).ThenBy(x => x.SortOrder).ThenBy(x => x.Id);
}

public sealed record PhotoAlbumRequest(string Name, string? Description = null, string? Image = null, string? Language = null, int SortOrder = 0, int Status = IHasStatus.Active);

public sealed record PhotoAlbumDto(
    long Id, Guid PublicId, string Name, string? Description, string? Image, string? Language, int SortOrder, int Status, int PhotoCount, DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Album ảnh (LRC: PhotoAlbumController, quyền PORTAL_GALLERY).</summary>
public sealed class PhotoAlbumResource(ICrudDbContext db, ITenantContext tenant, IOptions<PortalOptions> options)
    : CrudResource<PhotoAlbumResource, PhotoAlbum, CrudSearch, PhotoAlbumRequest, PhotoAlbumDto>(db)
{
    private readonly PortalOptions _options = options.Value;

    protected override string EntityName => "Album ảnh";

    protected override string? Describe(PhotoAlbum entity) => entity.Name;

    protected override Expression<Func<PhotoAlbum, PhotoAlbumDto>> Projection => x => new PhotoAlbumDto(
        x.Id, x.PublicId, x.Name, x.Description, x.Image, x.Language, x.SortOrder, x.Status, Db.Set<Photo>().Count(p => p.PhotoAlbumId == x.Id), x.CreatedAt, x.UpdatedAt);

    protected override PhotoAlbum Create(PhotoAlbumRequest r) =>
        PhotoAlbum.Create(r.Name, r.Description, r.Image, r.Language, r.SortOrder, r.Status, _options.MediaPublicPrefix, tenant.RequireTenantId());

    protected override void Update(PhotoAlbum entity, PhotoAlbumRequest r) =>
        entity.Update(r.Name, r.Description, r.Image, r.Language, r.SortOrder, r.Status, _options.MediaPublicPrefix, tenant.RequireTenantId());

    protected override async Task EnsureDeletableAsync(PhotoAlbum entity, CancellationToken ct)
    {
        if (await Db.Set<Photo>().AnyAsync(p => p.PhotoAlbumId == entity.Id, ct)) throw new ConflictException("ALBUM_HAS_PHOTOS", $"Album '{entity.Name}' còn ảnh.");
    }

    protected override IQueryable<PhotoAlbum> Filter(IQueryable<PhotoAlbum> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<PhotoAlbum> Order(IQueryable<PhotoAlbum> query) => query.OrderBy(x => x.SortOrder).ThenByDescending(x => x.Id);
}

public sealed class PhotoSearch : CrudSearch
{
    public long? PhotoAlbumId { get; set; }
}

public sealed record PhotoRequest(long PhotoAlbumId, string Name, string Image, string? Brief = null, string? Link = null, int SortOrder = 0, int Status = IHasStatus.Active);

public sealed record PhotoDto(
    long Id, Guid PublicId, long PhotoAlbumId, string Name, string? Brief, string Image, string? Link, int SortOrder, int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Ảnh (LRC: PhotoController, quyền PORTAL_GALLERY).</summary>
public sealed class PhotoResource(ICrudDbContext db, ITenantContext tenant, IOptions<PortalOptions> options) : CrudResource<PhotoResource, Photo, PhotoSearch, PhotoRequest, PhotoDto>(db)
{
    private readonly PortalOptions _options = options.Value;

    protected override string EntityName => "Ảnh";

    protected override string? Describe(Photo entity) => entity.Name;

    protected override Expression<Func<Photo, PhotoDto>> Projection => x =>
        new PhotoDto(x.Id, x.PublicId, x.PhotoAlbumId, x.Name, x.Brief, x.Image, x.Link, x.SortOrder, x.Status, x.CreatedAt, x.UpdatedAt);

    protected override Photo Create(PhotoRequest r) =>
        Photo.Create(r.PhotoAlbumId, r.Name, r.Brief, r.Image, r.Link, r.SortOrder, r.Status, _options.MediaPublicPrefix, tenant.RequireTenantId());

    protected override void Update(Photo entity, PhotoRequest r) =>
        entity.Update(r.Name, r.Brief, r.Image, r.Link, r.SortOrder, r.Status, _options.MediaPublicPrefix, tenant.RequireTenantId());

    protected override async Task ValidateAsync(Photo entity, CancellationToken ct)
    {
        if (!await Db.Set<PhotoAlbum>().AnyAsync(a => a.Id == entity.PhotoAlbumId, ct)) throw new BusinessRuleException("ALBUM_NOT_FOUND", "Album đã chọn không còn.");
    }

    protected override IQueryable<Photo> Filter(IQueryable<Photo> query, PhotoSearch search)
    {
        if (search.PhotoAlbumId is { } album) query = query.Where(x => x.PhotoAlbumId == album);
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<Photo> Order(IQueryable<Photo> query) => query.OrderBy(x => x.PhotoAlbumId).ThenBy(x => x.SortOrder).ThenBy(x => x.Id);
}

public sealed record VideoRequest(string Name, string Link, string? Description = null, string? Images = null, string? Language = null, int SortOrder = 0, int Status = IHasStatus.Active);

public sealed record VideoDto(
    long Id, Guid PublicId, string Name, string? Description, string Provider, string VideoId, string EmbedUrl, string? Images, string? Language, int SortOrder, int Status,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Video (LRC: VideoController, quyền PORTAL_GALLERY) — chỉ nhúng YouTube/Vimeo.</summary>
public sealed class VideoResource(ICrudDbContext db, ITenantContext tenant, IOptions<PortalOptions> options) : CrudResource<VideoResource, Video, CrudSearch, VideoRequest, VideoDto>(db)
{
    private readonly PortalOptions _options = options.Value;

    protected override string EntityName => "Video";

    protected override string? Describe(Video entity) => entity.Name;

    // EmbedUrl dựng từ Provider + VideoId đã kiểm; EF không dịch được thuộc tính tính toán nên tách ra ở ToDto.
    protected override Expression<Func<Video, VideoDto>> Projection => x => new VideoDto(
        x.Id, x.PublicId, x.Name, x.Description, x.Provider, x.VideoId,
        x.Provider == "vimeo" ? "https://player.vimeo.com/video/" + x.VideoId : "https://www.youtube-nocookie.com/embed/" + x.VideoId,
        x.Images, x.Language, x.SortOrder, x.Status, x.CreatedAt, x.UpdatedAt);

    protected override Video Create(VideoRequest r) =>
        Video.Create(r.Name, r.Description, r.Link, r.Images, r.Language, r.SortOrder, r.Status, _options.MediaPublicPrefix, tenant.RequireTenantId());

    protected override void Update(Video entity, VideoRequest r) =>
        entity.Update(r.Name, r.Description, r.Link, r.Images, r.Language, r.SortOrder, r.Status, _options.MediaPublicPrefix, tenant.RequireTenantId());

    protected override IQueryable<Video> Filter(IQueryable<Video> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<Video> Order(IQueryable<Video> query) => query.OrderBy(x => x.SortOrder).ThenByDescending(x => x.Id);
}
