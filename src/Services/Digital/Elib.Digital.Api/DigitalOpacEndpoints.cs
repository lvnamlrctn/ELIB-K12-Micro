using Elib.Digital.Application;

namespace Elib.Digital.Api;

/// <summary>
/// OPAC tài liệu số, ẩn danh (gateway: /api/opac/digital/browse/** → /api/opac/**, công khai theo host đơn vị — license DIGITAL kiểm
/// ở gateway, không kiểm lại ở đây, đúng khuôn Catalog/Search's nhóm /api/opac). Duyệt/tìm/chi tiết; mượn/trả/đặt trước ở lát sau
/// (ReaderDigitalEndpoints, cần đăng nhập bạn đọc).
/// </summary>
public static class DigitalOpacEndpoints
{
    public static IEndpointRouteBuilder MapDigitalOpacEndpoints(this IEndpointRouteBuilder app)
    {
        var opac = app.MapGroup("/api/opac").WithTags("Opac").AllowAnonymous();

        opac.MapPost("/items/Search", (OpacItemSearch search, OpacItems items, CancellationToken ct) => items.SearchAsync(search, ct));
        opac.MapGet("/items/{publicId:guid}", (Guid publicId, OpacItems items, CancellationToken ct) => items.GetAsync(publicId, ct));

        opac.MapGet("/collections", (OpacCatalogs catalogs, CancellationToken ct) => catalogs.CollectionsAsync(ct));
        opac.MapGet("/subjects", (OpacCatalogs catalogs, CancellationToken ct) => catalogs.SubjectsAsync(ct));
        opac.MapGet("/topics", (OpacCatalogs catalogs, CancellationToken ct) => catalogs.TopicsAsync(ct));

        return app;
    }
}
