using Ganss.Xss;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Portal.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Portal.Application;

public sealed class PortalOptions
{
    public const string SectionName = "Portal";

    public string MediaPublicPrefix { get; set; } = "/s3/media-public";
}

/// <summary>
/// Làm sạch HTML của CMS (tin, trang) — chạy ở server TRƯỚC KHI lưu; OPAC chỉ hiển thị nội dung đã sạch (và vẫn qua DomSanitizer của Angular).
/// Danh sách trắng thẻ/thuộc tính; không cho script, iframe, form, style nội tuyến, sự kiện on*, javascript:/data:. Ảnh chỉ nhận ảnh đã upload
/// của chính đơn vị (đường dẫn media công khai): OPAC có CSP img-src 'self' nên ảnh ngoài vốn không hiện, và không để lộ IP bạn đọc cho site ngoài.
/// </summary>
public interface IHtmlCleaner
{
    string? Clean(string? html, long tenantId);
}

public sealed class HtmlCleaner(Microsoft.Extensions.Options.IOptions<PortalOptions> options) : IHtmlCleaner
{
    private static readonly string[] Tags =
    [
        "p", "br", "hr", "strong", "b", "em", "i", "u", "s", "sub", "sup", "span", "div", "h2", "h3", "h4", "h5", "ul", "ol", "li", "blockquote", "pre", "code",
        "a", "img", "figure", "figcaption", "table", "thead", "tbody", "tfoot", "tr", "th", "td", "caption",
    ];

    private static readonly string[] Attributes = ["href", "src", "alt", "title", "width", "height", "colspan", "rowspan", "target", "rel", "class"];

    /// <summary>Lớp CSS duy nhất được giữ lại: vị trí ảnh do trình soạn đặt (không cho style nội tuyến). Kích thước là thuộc tính width (số nguyên, kiểm bên dưới).</summary>
    private static readonly string[] ImageClasses = ["img-left", "img-center", "img-right"];

    public string? Clean(string? html, long tenantId)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var imagePrefix = $"{options.Value.MediaPublicPrefix.TrimEnd('/')}/{tenantId}/{PortalMedia.ImageCode}/";
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in Tags) sanitizer.AllowedTags.Add(tag);
        sanitizer.AllowedAttributes.Clear();
        foreach (var attribute in Attributes) sanitizer.AllowedAttributes.Add(attribute);
        sanitizer.AllowedSchemes.Clear();
        foreach (var scheme in (string[])["http", "https", "mailto", "tel"]) sanitizer.AllowedSchemes.Add(scheme);
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedClasses.Clear();
        foreach (var cssClass in ImageClasses) sanitizer.AllowedClasses.Add(cssClass);
        sanitizer.AllowDataAttributes = false;
        sanitizer.FilterUrl += (_, e) =>
        {
            // Ảnh: chỉ ảnh media công khai của đơn vị. Liên kết: http(s)/mailto/tel hoặc đường dẫn nội bộ "/…" (không "//").
            var url = e.OriginalUrl ?? "";
            if (string.Equals(e.Tag.TagName, "IMG", StringComparison.OrdinalIgnoreCase))
                e.SanitizedUrl = url.StartsWith(imagePrefix, StringComparison.Ordinal) ? e.SanitizedUrl : null;
            else if (url.StartsWith("//", StringComparison.Ordinal) || url.Contains('\\', StringComparison.Ordinal))
                e.SanitizedUrl = null;
        };
        sanitizer.PostProcessNode += (_, e) =>
        {
            // Ảnh bị loại nguồn (ngoài đơn vị, data:…) → bỏ luôn thẻ img, không để lại thẻ rỗng.
            if (e.Node is AngleSharp.Dom.IElement { LocalName: "img" } image && string.IsNullOrEmpty(image.GetAttribute("src")))
                e.ReplacementNodes.Add(e.Document.CreateTextNode(string.Empty));
            else if (e.Node is AngleSharp.Dom.IElement { LocalName: "img" } sized)
            {
                // Chiều rộng do kéo góc ảnh: số nguyên 16–4000 điểm ảnh; chiều cao bỏ để trình duyệt giữ tỉ lệ.
                sized.RemoveAttribute("height");
                if (!int.TryParse(sized.GetAttribute("width"), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var width) || width is < 16 or > 4000)
                    sized.RemoveAttribute("width");
            }
            else if (e.Node is AngleSharp.Dom.IElement { LocalName: "a" } link && link.GetAttribute("href") is { } href && href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                link.SetAttribute("target", "_blank");
                link.SetAttribute("rel", "noopener noreferrer nofollow");
            }
            else if (e.Node is AngleSharp.Dom.IElement { LocalName: "a" } other)
            {
                other.RemoveAttribute("target");
            }
        };
        var clean = sanitizer.Sanitize(html);
        return string.IsNullOrWhiteSpace(clean) ? null : clean;
    }
}

/// <summary>
/// Đăng / gỡ / sửa nội dung ĐÃ ĐĂNG cần quyền <c>publish</c> của module (tin, trang) — người chỉ có quyền Sửa soạn được bản nháp. Cùng khuôn "duyệt theo
/// thẩm quyền" của khung phê duyệt: vai trò <c>*</c> luôn có.
/// </summary>
internal static class PublishGuard
{
    public static async Task RequireAsync(IActorPermissions permissions, string module, CancellationToken ct)
    {
        if (!await permissions.HasAsync($"{module}:publish", ct))
            throw new BusinessRuleException(ElibErrorCodes.PermissionDenied, "Chỉ người có quyền Đăng bài mới đăng, gỡ hoặc sửa nội dung đã đăng.", 403);
    }
}

public interface IPortalTreeRequest
{
    long? ParentId { get; }
    int? SortOrder { get; }
    int? Status { get; }
}

public sealed class PortalTreeSearch : CrudSearch
{
    public long? ParentId { get; set; }
    public string? Language { get; set; }

    /// <summary>Chỉ menu dùng: lọc theo loại menu.</summary>
    public long? MenuTypeId { get; set; }
}

/// <summary>
/// Danh mục cây dùng chung bởi chuyên mục tin và menu (cùng khuôn Digital's TreeCatalogResource): cha/Level/thứ tự/trạng thái, chống vòng lặp,
/// chặn xoá khi còn mục con. Lớp con chỉ khai báo tạo/áp trường riêng.
/// </summary>
public abstract class PortalTreeResource<TSelf, TEntity, TRequest, TDto>(ICrudDbContext db)
    : CrudResource<TSelf, TEntity, PortalTreeSearch, TRequest, TDto>(db)
    where TSelf : PortalTreeResource<TSelf, TEntity, TRequest, TDto>
    where TEntity : PortalTreeItem
    where TRequest : class, IPortalTreeRequest
{
    protected abstract TEntity NewEntity(TRequest request);

    protected abstract void ApplyFields(TEntity entity, TRequest request);

    protected sealed override TEntity Create(TRequest request) => throw new NotSupportedException();

    protected sealed override void Update(TEntity entity, TRequest request) => throw new NotSupportedException();

    protected override async Task<TEntity> CreateAsync(TRequest request, CancellationToken ct)
    {
        var parent = await ParentAsync(request.ParentId, ct);
        var entity = NewEntity(request);
        entity.AttachTo(parent);
        entity.Reorder(request.SortOrder ?? 0);
        if (request.Status is { } status) entity.ChangeStatus(status);
        return entity;
    }

    protected override async Task UpdateAsync(TEntity entity, TRequest request, CancellationToken ct)
    {
        ApplyFields(entity, request);
        entity.Reorder(request.SortOrder ?? entity.SortOrder);
        if (request.Status is { } status) entity.ChangeStatus(status);
        if (request.ParentId != entity.ParentId) await MoveAsync(entity, request.ParentId, ct);
    }

    protected override IQueryable<TEntity> Filter(IQueryable<TEntity> query, PortalTreeSearch search)
    {
        if (search.ParentId is { } parentId) query = query.Where(x => x.ParentId == parentId);
        if (!string.IsNullOrWhiteSpace(search.Language)) query = query.Where(x => x.Language == null || x.Language == search.Language);
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<TEntity> Order(IQueryable<TEntity> query) => query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id);

    protected override async Task EnsureDeletableAsync(TEntity entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.ParentId == entity.Id, ct))
            throw new ConflictException("TREE_HAS_CHILDREN", $"{EntityName} '{entity.Name}' còn mục con, không xoá được.");
    }

    private async Task<TEntity?> ParentAsync(long? parentId, CancellationToken ct) =>
        parentId is { } id and > 0
            ? await Set.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException($"{EntityName} cha", id)
            : null;

    private async Task MoveAsync(TEntity entity, long? newParentId, CancellationToken ct)
    {
        var parent = await ParentAsync(newParentId, ct);
        var descendants = await DescendantsAsync(entity.Id, ct);
        if (parent is not null && descendants.Any(d => d.Id == parent.Id))
            throw new BusinessRuleException("TREE_MOVE_CYCLE", $"Không chuyển {EntityName} vào chính nhánh con của nó.");
        var before = entity.Level;
        entity.AttachTo(parent);
        var delta = entity.Level - before;
        if (delta != 0)
            foreach (var d in descendants) d.ShiftLevel(delta);
    }

    private async Task<List<TEntity>> DescendantsAsync(long rootId, CancellationToken ct)
    {
        var all = await Set.Where(x => x.ParentId != null).ToListAsync(ct);
        var children = all.ToLookup(x => x.ParentId!.Value);
        var result = new List<TEntity>();
        var stack = new Stack<long>([rootId]);
        while (stack.Count > 0)
            foreach (var child in children[stack.Pop()])
            {
                result.Add(child);
                stack.Push(child.Id);
            }
        return result;
    }
}
