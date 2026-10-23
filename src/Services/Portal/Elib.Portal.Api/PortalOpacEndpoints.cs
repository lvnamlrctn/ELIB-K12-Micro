using Elib.Portal.Application;

namespace Elib.Portal.Api;

/// <summary>
/// Nội dung CMS công khai cho OPAC (gateway: /api/opac/portal/** → /api/opac/**, ẩn danh theo host đơn vị, giới hạn tần suất "opac", cần license PORTAL ở gateway).
/// Chỉ đọc nội dung đã đăng; ghi duy nhất là bình luận (chờ duyệt), liên hệ và bộ đếm truy cập — đều chống bot (ô bẫy) và có trần số lượng chưa xử lý.
/// </summary>
public static class PortalOpacEndpoints
{
    public static IEndpointRouteBuilder MapPortalOpacEndpoints(this IEndpointRouteBuilder app)
    {
        var opac = app.MapGroup("/api/opac").WithTags("Opac").AllowAnonymous();

        opac.MapGet("/home", (string? lang, OpacPortal p, CancellationToken ct) => p.HomeAsync(lang, ct));
        opac.MapGet("/menus", (string? lang, OpacPortal p, CancellationToken ct) => p.MenusAsync(lang, ct));
        opac.MapGet("/categories", (string? lang, OpacPortal p, CancellationToken ct) => p.CategoriesAsync(lang, ct));
        opac.MapPost("/news/Search", (OpacNewsSearch search, OpacPortal p, CancellationToken ct) => p.SearchNewsAsync(search, ct));
        opac.MapGet("/news/{slug}", (string slug, string? lang, OpacPortal p, CancellationToken ct) => p.NewsAsync(slug, lang, ct));
        opac.MapPost("/news/{slug}/view", async (string slug, OpacPortal p, CancellationToken ct) =>
        {
            await p.ViewAsync(slug, ct);
            return Results.NoContent();
        });
        opac.MapGet("/news/{slug}/comments", (string slug, OpacPortal p, CancellationToken ct) => p.CommentsAsync(slug, ct));
        opac.MapPost("/news/{slug}/comments", async (string slug, OpacCommentSubmission submission, OpacPortal p, CancellationToken ct) =>
        {
            await p.CommentAsync(slug, submission, ct);
            return Results.Accepted();
        });
        opac.MapGet("/pages/{slug}", (string slug, string? lang, OpacPortal p, CancellationToken ct) => p.PageAsync(slug, lang, ct));
        opac.MapGet("/banners", (string? position, string? lang, OpacPortal p, CancellationToken ct) => p.BannersAsync(position, lang, ct));
        opac.MapGet("/links", (string? lang, OpacPortal p, CancellationToken ct) => p.LinksAsync(lang, ct));
        opac.MapGet("/albums", (string? lang, int? take, OpacPortal p, CancellationToken ct) => p.AlbumsAsync(lang, take ?? 24, ct));
        opac.MapGet("/albums/{publicId:guid}/photos", (Guid publicId, OpacPortal p, CancellationToken ct) => p.PhotosAsync(publicId, ct));
        opac.MapGet("/videos", (string? lang, int? take, OpacPortal p, CancellationToken ct) => p.VideosAsync(lang, take ?? 24, ct));
        opac.MapGet("/contact-groups", (string? lang, OpacPortal p, CancellationToken ct) => p.ContactGroupsAsync(lang, ct));
        opac.MapPost("/contacts", async (OpacContactSubmission submission, OpacPortal p, CancellationToken ct) =>
        {
            await p.SubmitContactAsync(submission, ct);
            return Results.Accepted();
        });
        opac.MapPost("/counter/hit", (VisitCounter counter, CancellationToken ct) => counter.HitAsync(ct));
        opac.MapGet("/counter", (VisitCounter counter, CancellationToken ct) => counter.SummaryAsync(ct));
        return app;
    }
}
