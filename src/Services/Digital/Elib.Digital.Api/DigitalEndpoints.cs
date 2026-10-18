using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.Digital.Application;

namespace Elib.Digital.Api;

/// <summary>
/// Tài liệu số (gateway: /api/admin/digital/** → /api/**, cần license DIGITAL). MVP tham khảo ELIB-LRC: bộ sưu tập/môn loại/chủ đề/loại
/// tài liệu, biểu ghi số + file, chính sách mượn, mượn/trả/đặt trước. Danh mục và nghiệp vụ được thêm dần theo từng lát (xem plan).
/// </summary>
public static class DigitalEndpoints
{
    public const string Module = "DIGITAL";

    public static IEndpointRouteBuilder MapDigitalEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(new RequiresModuleAttribute(Module));

        api.MapCrud<EbookCollectionResource>("/collections", "DIGITAL_COLLECTIONS").WithTags("Collections");
        api.MapCrud<EbookSubjectResource>("/subjects", "DIGITAL_SUBJECTS").WithTags("Subjects");
        api.MapCrud<EbookTopicResource>("/topics", "DIGITAL_TOPICS").WithTags("Topics");
        api.MapCrud<EbookDigTypeResource>("/dig-types", "DIGITAL_DIG_TYPES").WithTags("DigTypes");
        api.MapCrud<PolicyDigitalResource>("/policies", "DIGITAL_POLICIES").WithTags("Policies");
        api.MapCrud<PolicyDigitalByCollectionResource>("/policies-by-collection", "DIGITAL_POLICIES_BY_COLLECTION").WithTags("PoliciesByCollection");
        var items = api.MapCrud<EbookItemResource>("/items", "DIGITAL_ITEMS").WithTags("Items");
        api.MapCrud<EbookFileResource>("/files", "DIGITAL_ITEMS").WithTags("Files");

        // Ô chọn liên kết sách in của màn thêm/sửa tài liệu số (quyết định #2) — proxy sang catalog, không giữ bản sao.
        items.MapGet("/bib-options", [Permission("DIGITAL_ITEMS", "view")] (string? q, ICatalogBibLookup bibs, CancellationToken ct) =>
            bibs.SearchAsync(q, ct)).WithTags("Items");

        // Lượt mượn/đặt trước: cán bộ chỉ tra cứu + thu hồi — bạn đọc tự mượn/trả/đặt trước/huỷ qua OPAC (ReaderDigitalEndpoints).
        var loans = api.MapGroup("/loans").WithTags("Loans");
        loans.MapPost("/Search", [Permission("DIGITAL_LOANS", "view")] (EbookLoanSearch search, EbookLoanResource r, CancellationToken ct)
            => r.SearchAsync(search, ct));
        loans.MapGet("/GetById/{publicId:guid}", [Permission("DIGITAL_LOANS", "view")] (Guid publicId, EbookLoanResource r, CancellationToken ct)
            => r.GetAsync(publicId, ct));
        loans.MapPost("/Recall", [Permission("DIGITAL_LOANS", "edit")] (RecallLoanRequest request, EbookLoanResource r, ICurrentActor actor, CancellationToken ct)
            => r.RecallAsync(request, actor.Id, ct));
        loans.MapPost("/RecallAll", [Permission("DIGITAL_LOANS", "edit")] (RecallAllRequest request, EbookLoanResource r, ICurrentActor actor, CancellationToken ct)
            => r.RecallAllAsync(request, actor.Id, ct));

        var reservations = api.MapGroup("/reservations").WithTags("Reservations");
        reservations.MapPost("/Search", [Permission("DIGITAL_LOANS", "view")] (EbookReservationSearch search, EbookReservationResource r, CancellationToken ct)
            => r.SearchAsync(search, ct));
        reservations.MapGet("/GetById/{publicId:guid}", [Permission("DIGITAL_LOANS", "view")] (Guid publicId, EbookReservationResource r, CancellationToken ct)
            => r.GetAsync(publicId, ct));

        return app;
    }
}
