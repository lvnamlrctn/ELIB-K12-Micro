using Elib.BuildingBlocks.Domain;

namespace Elib.Portal.Domain;

/// <summary>Vị trí banner (LRC: PublicHomeBanner = banner trang chủ).</summary>
public static class BannerPositions
{
    public const string Home = "home";
    public const string Top = "top";

    public static readonly IReadOnlyList<string> All = [Home, Top];
}

/// <summary>Banner (LRC: Banner) — ảnh trượt trên trang chủ. Hiện khi Đang hoạt động và nằm trong khoảng ngày hiển thị.</summary>
public sealed class Banner : TenantEntity, IHasStatus
{
    private Banner() { }

    public string Name { get; private set; } = "";
    public string Position { get; private set; } = BannerPositions.Home;
    public string Image { get; private set; } = "";
    public string? Link { get; private set; }
    public int? Width { get; private set; }
    public int? Height { get; private set; }
    public int SortOrder { get; private set; }
    public string? Language { get; private set; }

    /// <summary>Khoảng ngày hiển thị (ngày lịch giờ Việt Nam, gồm cả hai đầu; null = không giới hạn).</summary>
    public DateOnly? ShowFrom { get; private set; }

    public DateOnly? ShowTo { get; private set; }
    public int Status { get; private set; } = IHasStatus.Active;

    public static Banner Create(BannerFields f, string mediaPublicPrefix, long tenantId)
    {
        var banner = new Banner();
        banner.Update(f, mediaPublicPrefix, tenantId);
        return banner;
    }

    public void Update(BannerFields f, string mediaPublicPrefix, long tenantId)
    {
        ArgumentNullException.ThrowIfNull(f);
        Name = TextRules.Required(f.Name, 250, "NAME_INVALID", "Tên banner");
        Position = BannerPositions.All.FirstOrDefault(p => string.Equals(p, f.Position ?? BannerPositions.Home, StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("BANNER_POSITION_INVALID", "Vị trí banner không hợp lệ (home, top).");
        Image = PortalMedia.Image(f.Image, mediaPublicPrefix, tenantId, "Ảnh banner") ?? throw new BusinessRuleException("MEDIA_URL_INVALID", "Ảnh banner bắt buộc.");
        Link = SafeLink.Validate(f.Link, "Liên kết");
        Width = Dimension(f.Width);
        Height = Dimension(f.Height);
        SortOrder = f.SortOrder;
        Language = TextRules.Language(f.Language);
        if (f.ShowFrom is { } from && f.ShowTo is { } to && to < from) throw new BusinessRuleException("TIME_RANGE_INVALID", "Ngày kết thúc không được trước ngày bắt đầu.");
        ShowFrom = f.ShowFrom;
        ShowTo = f.ShowTo;
        Status = StatusRules.Validate(f.Status);
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);

    public bool IsVisible(DateOnly today) => Status == IHasStatus.Active && (ShowFrom is null || ShowFrom <= today) && (ShowTo is null || ShowTo >= today);

    internal static int? Dimension(int? value) =>
        value is null or (>= 1 and <= 10_000) ? value : throw new BusinessRuleException("DIMENSION_INVALID", "Kích thước ảnh phải từ 1 đến 10000.");
}

public sealed record BannerFields(
    string Name, string Image, string? Position = null, string? Link = null, int? Width = null, int? Height = null, int SortOrder = 0, string? Language = null,
    DateOnly? ShowFrom = null, DateOnly? ShowTo = null, int Status = IHasStatus.Active);

/// <summary>Nhóm quảng cáo (LRC: ADSGroup) — vùng đặt quảng cáo (ví dụ cột phải).</summary>
public sealed class AdsGroup : TenantEntity, IHasStatus
{
    private AdsGroup() { }

    public string Name { get; private set; } = "";
    public string? Type { get; private set; }
    public int? Width { get; private set; }
    public int? Height { get; private set; }
    public string? Language { get; private set; }
    public int Status { get; private set; } = IHasStatus.Active;

    public static AdsGroup Create(string? name, string? type, int? width, int? height, string? language, int status)
    {
        var group = new AdsGroup();
        group.Update(name, type, width, height, language, status);
        return group;
    }

    public void Update(string? name, string? type, int? width, int? height, string? language, int status)
    {
        Name = TextRules.Required(name, 200, "NAME_INVALID", "Tên nhóm quảng cáo");
        Type = TextRules.Optional(type, 50, "TYPE_INVALID", "Loại");
        Width = Banner.Dimension(width);
        Height = Banner.Dimension(height);
        Language = TextRules.Language(language);
        Status = StatusRules.Validate(status);
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);
}

/// <summary>Quảng cáo (LRC: ADS) — ảnh + liên kết trong một nhóm.</summary>
public sealed class Ads : TenantEntity, IHasStatus
{
    private Ads() { }

    public long AdsGroupId { get; private set; }
    public string Name { get; private set; } = "";
    public string Image { get; private set; } = "";
    public string? Link { get; private set; }
    public int? Width { get; private set; }
    public int? Height { get; private set; }
    public int SortOrder { get; private set; }
    public DateOnly? ShowFrom { get; private set; }
    public DateOnly? ShowTo { get; private set; }
    public int Status { get; private set; } = IHasStatus.Active;

    public static Ads Create(long groupId, string? name, string? image, string? link, int? width, int? height, int sortOrder, DateOnly? showFrom, DateOnly? showTo,
        int status, string mediaPublicPrefix, long tenantId)
    {
        var ads = new Ads { AdsGroupId = groupId };
        ads.Update(name, image, link, width, height, sortOrder, showFrom, showTo, status, mediaPublicPrefix, tenantId);
        return ads;
    }

    public void Update(string? name, string? image, string? link, int? width, int? height, int sortOrder, DateOnly? showFrom, DateOnly? showTo, int status,
        string mediaPublicPrefix, long tenantId)
    {
        Name = TextRules.Required(name, 250, "NAME_INVALID", "Tên quảng cáo");
        Image = PortalMedia.Image(image, mediaPublicPrefix, tenantId, "Ảnh quảng cáo") ?? throw new BusinessRuleException("MEDIA_URL_INVALID", "Ảnh quảng cáo bắt buộc.");
        Link = SafeLink.Validate(link, "Liên kết");
        Width = Banner.Dimension(width);
        Height = Banner.Dimension(height);
        SortOrder = sortOrder;
        if (showFrom is { } from && showTo is { } to && to < from) throw new BusinessRuleException("TIME_RANGE_INVALID", "Ngày kết thúc không được trước ngày bắt đầu.");
        ShowFrom = showFrom;
        ShowTo = showTo;
        Status = StatusRules.Validate(status);
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);

    public bool IsVisible(DateOnly today) => Status == IHasStatus.Active && (ShowFrom is null || ShowFrom <= today) && (ShowTo is null || ShowTo >= today);
}
