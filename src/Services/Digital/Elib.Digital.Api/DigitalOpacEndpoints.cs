using System.Security.Claims;
using Elib.BuildingBlocks.Tenancy;
using Elib.Digital.Application;

namespace Elib.Digital.Api;

/// <summary>
/// OPAC tài liệu số, ẩn danh (gateway: /api/opac/digital/browse/** → /api/opac/**, công khai theo host đơn vị — license DIGITAL kiểm
/// ở gateway, không kiểm lại ở đây, đúng khuôn Catalog/Search's nhóm /api/opac). Duyệt/tìm/chi tiết/URL file; mượn/trả/đặt trước ở
/// <see cref="ReaderDigitalEndpoints"/> (cần đăng nhập bạn đọc).
/// </summary>
public static class DigitalOpacEndpoints
{
    public static IEndpointRouteBuilder MapDigitalOpacEndpoints(this IEndpointRouteBuilder app)
    {
        var opac = app.MapGroup("/api/opac").WithTags("Opac").AllowAnonymous();

        opac.MapPost("/items/Search", (OpacItemSearch search, OpacItems items, CancellationToken ct) => items.SearchAsync(search, ct));
        opac.MapGet("/items/{publicId:guid}", (Guid publicId, OpacItems items, CancellationToken ct) => items.GetAsync(publicId, ct));

        // URL đọc/tải file — token bạn đọc gắn kèm nếu có (route nằm trong READER_APIS) nhưng không bắt buộc: tài liệu miễn phí
        // (Free) đọc được không cần đăng nhập, còn lại EbookFileAccess tự đòi quyền mượn.
        opac.MapGet("/items/{publicId:guid}/files/{filePublicId:guid}/url",
            (Guid publicId, Guid filePublicId, bool? download, ClaimsPrincipal principal, EbookFileAccess access, CancellationToken ct) =>
                access.SignedUrlAsync(publicId, filePublicId, ReaderIdOrNull(principal), download ?? false, ct));

        opac.MapPost("/items/{publicId:guid}/reviews", (Guid publicId, OpacReviewSubmission submission, OpacReviews reviews, CancellationToken ct) =>
            reviews.SubmitAsync(publicId, submission, ct));
        opac.MapGet("/items/{publicId:guid}/reviews", (Guid publicId, OpacReviews reviews, CancellationToken ct) => reviews.PublishedAsync(publicId, ct));

        // Liên kết ngược từ trang chi tiết sách in (catalog) — "Có bản điện tử". Null (204) nếu không có/chưa xuất bản, không 404
        // (không phải lỗi — phần lớn sách in không có bản điện tử liên kết).
        opac.MapGet("/items/by-bib/{bibPublicId:guid}", async (Guid bibPublicId, OpacItems items, CancellationToken ct) =>
        {
            var found = await items.FindByLinkedBibAsync(bibPublicId, ct);
            return found is null ? Results.NoContent() : Results.Ok(found);
        });

        opac.MapGet("/collections", (OpacCatalogs catalogs, CancellationToken ct) => catalogs.CollectionsAsync(ct));
        opac.MapGet("/subjects", (OpacCatalogs catalogs, CancellationToken ct) => catalogs.SubjectsAsync(ct));
        opac.MapGet("/topics", (OpacCatalogs catalogs, CancellationToken ct) => catalogs.TopicsAsync(ct));
        // Cho dropdown loại tài liệu của form nộp tài liệu bạn đọc — không cần đăng nhập để xem danh mục trước khi vào form.
        opac.MapGet("/dig-types", (OpacCatalogs catalogs, CancellationToken ct) => catalogs.DigTypesAsync(ct));

        return app;
    }

    private static Guid? ReaderIdOrNull(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ElibClaimTypes.ReaderId), out var id) ? id : null;
}
