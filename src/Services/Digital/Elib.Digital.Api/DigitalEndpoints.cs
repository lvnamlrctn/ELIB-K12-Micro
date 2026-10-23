using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Digital;
using Elib.Contracts.Events.Platform;
using Elib.Digital.Application;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Elib.Digital.Api;

/// <summary>
/// Tài liệu số (gateway: /api/admin/digital/** → /api/**, cần license DIGITAL). MVP tham khảo ELIB-LRC: bộ sưu tập/môn loại/chủ đề/loại
/// tài liệu, biểu ghi số + file, chính sách mượn, mượn/trả/đặt trước. Danh mục và nghiệp vụ được thêm dần theo từng lát (xem plan).
/// </summary>
public static class DigitalEndpoints
{
    public const string Module = "DIGITAL";

    /// <summary>Trần dung lượng gói ZIP nhập DSpace (nginx và gateway phải cho phép bằng hoặc lớn hơn).</summary>
    public const long MaxDspaceUploadBytes = 512L * 1024 * 1024;

    private static ItemImportDefaults ImportDefaults(IFormCollection form)
    {
        static long? Id(string? v) => long.TryParse(v, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) ? id : null;
        static bool Flag(string? v) => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || v == "1";
        return new ItemImportDefaults(Id(form["collectionId"]), Id(form["subjectId"]), Id(form["topicId"]), Flag(form["published"]), Flag(form["free"]),
            Flag(form["allowDownload"]), string.IsNullOrWhiteSpace(form["language"]) ? null : form["language"].ToString());
    }

    public static IEndpointRouteBuilder MapDigitalEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(new RequiresModuleAttribute(Module));

        var collections = api.MapCrud<EbookCollectionResource>("/collections", "DIGITAL_COLLECTIONS").WithTags("Collections");
        collections.MapGet("/{publicId:guid}/permissions", [Permission("DIGITAL_COLLECTIONS", "view")] (Guid publicId, PolicyDigitalByCollectionResource r, CancellationToken ct)
            => r.PermissionsAsync(publicId, ct));
        collections.MapPut("/{publicId:guid}/permissions", [Permission("DIGITAL_COLLECTIONS", "edit")] (Guid publicId, IReadOnlyList<CollectionPermissionRow> rows, PolicyDigitalByCollectionResource r, CancellationToken ct)
            => r.SavePermissionsAsync(publicId, rows, ct));
        api.MapCrud<EbookSubjectResource>("/subjects", "DIGITAL_SUBJECTS").WithTags("Subjects");
        api.MapCrud<EbookTopicResource>("/topics", "DIGITAL_TOPICS").WithTags("Topics");
        api.MapCrud<EbookDigTypeResource>("/dig-types", "DIGITAL_DIG_TYPES").WithTags("DigTypes");
        api.MapCrud<PolicyDigitalResource>("/policies", "DIGITAL_POLICIES").WithTags("Policies");
        api.MapCrud<PolicyDigitalByCollectionResource>("/policies-by-collection", "DIGITAL_POLICIES_BY_COLLECTION").WithTags("PoliciesByCollection");
        var items = api.MapCrud<EbookItemResource>("/items", "DIGITAL_ITEMS").WithTags("Items");
        api.MapCrud<EbookFileResource>("/files", "DIGITAL_ITEMS").WithTags("Files");
        var reviews = api.MapCrud<EbookReviewResource>("/reviews", "DIGITAL_REVIEWS").WithTags("Reviews");
        reviews.MapPost("/{publicId:guid}/Approve", [Permission("DIGITAL_REVIEWS", "approve")] (Guid publicId, EbookReviewResource r, ICurrentActor actor, CancellationToken ct)
            => r.ApproveAsync(publicId, actor.Id, ct));
        reviews.MapPost("/{publicId:guid}/Reject", [Permission("DIGITAL_REVIEWS", "approve")] (Guid publicId, RejectReviewRequest request, EbookReviewResource r, ICurrentActor actor, CancellationToken ct)
            => r.RejectAsync(publicId, request, actor.Id, ct));

        // Theo dõi đọc (LRC: ReadingTrackingController) — thủ thư xem lượt đọc và thống kê.
        // Bảng điều khiển (tab Tài liệu số): cùng quyền xem tài liệu số.
        api.MapGet("/dashboard", [Permission("DIGITAL_ITEMS", "view")] (DigitalDashboardService s, CancellationToken ct) => s.BuildAsync(ct)).WithTags("Dashboard");

        var reading = api.MapGroup("/reading-tracking").WithTags("ReadingTracking");
        reading.MapPost("/Search", [Permission("DIGITAL_READING", "view")] (ReadingSearch search, ReadingTracking t, CancellationToken ct) => t.SearchAsync(search, ct));
        reading.MapPost("/Statistics", [Permission("DIGITAL_READING", "view")] (ReadingStatsRequest request, ReadingTracking t, CancellationToken ct) => t.StatisticsAsync(request, ct));

        api.MapCrud<MetadataSchemaResource>("/metadata-schemas", "DIGITAL_METADATA").WithTags("Metadata");
        api.MapCrud<MetadataFieldResource>("/metadata-fields", "DIGITAL_METADATA").WithTags("Metadata");
        // Metadata của một tài liệu số: đọc/đặt lại cả danh sách — cùng quyền với sửa tài liệu số.
        items.MapGet("/{publicId:guid}/metadata", [Permission("DIGITAL_ITEMS", "view")] (Guid publicId, EbookMetadata metadata, CancellationToken ct)
            => metadata.GetAsync(publicId, ct));
        items.MapPut("/{publicId:guid}/metadata", [Permission("DIGITAL_ITEMS", "edit")] (Guid publicId, EbookMetadataSave save, EbookMetadata metadata, CancellationToken ct)
            => metadata.ReplaceAsync(publicId, save, ct));

        // Thao tác hàng loạt (LRC: BulkMoveCollection + xoá nhiều): chọn theo mã các dòng đã tích hoặc theo bộ lọc hiện tại.
        items.MapPost("/Selection/Preview", [Permission("DIGITAL_ITEMS", "view")] (ItemSelection selection, EbookItemResource r, CancellationToken ct)
            => r.PreviewSelectionAsync(selection, ct));
        items.MapPut("/BulkMoveCollection", [Permission("DIGITAL_ITEMS", "edit")] (ItemBulkMoveRequest request, EbookItemResource r, CancellationToken ct)
            => r.BulkMoveAsync(request, ct));
        items.MapPut("/BulkStatus", [Permission("DIGITAL_ITEMS", "edit")] (ItemBulkStatusRequest request, EbookItemResource r, CancellationToken ct)
            => r.BulkStatusAsync(request, ct));
        items.MapPost("/BulkDelete", [Permission("DIGITAL_ITEMS", "delete")] (ItemSelection selection, EbookItemResource r, CancellationToken ct)
            => r.BulkDeleteAsync(selection, ct));

        items.MapPut("/{publicId:guid}/cover", [Permission("DIGITAL_ITEMS", "edit")] (Guid publicId, SetCoverRequest request, EbookItemResource r, CancellationToken ct)
            => r.SetCoverAsync(publicId, request.CoverUrl, ct));

        // Xuất/nhập (LRC: GetExportFields, ExportExcel, ExportMetaData, ImportExcel, ImportXml). Nhập dùng quyền thêm, xuất dùng quyền xem (như bạn đọc).
        items.MapGet("/GetExportFields", [Permission("DIGITAL_ITEMS", "view")] () => EbookItemResource.ExportFields);
        items.MapPost("/Export", [Permission("DIGITAL_ITEMS", "view")] async (ItemExportRequest request, EbookItemResource r, CancellationToken ct)
            => Results.File(await r.ExportAsync(request, ct), CrudExcel.ContentType, "danh-sach-tai-lieu.xlsx"));
        items.MapPost("/ExportMetadata", [Permission("DIGITAL_ITEMS", "view")] async (ItemExportRequest request, EbookItemResource r, TimeProvider clock, CancellationToken ct)
            => Results.File(await r.ExportMetadataAsync(request, ct), "application/zip", $"dspace-metadata-{clock.GetUtcNow():yyyyMMddHHmmss}.zip"));
        items.MapPost("/ImportExcel", [Permission("DIGITAL_ITEMS", "add")] async (HttpRequest http, EbookItemResource r, CancellationToken ct) =>
        {
            var form = await http.ReadFormAsync(ct);
            var file = form.Files.GetFile("file") ?? throw new BusinessRuleException("IMPORT_FILE_EMPTY", "Chưa chọn file Excel.");
            if (file.Length is 0 or > CrudExcel.MaxFileBytes)
                throw new BusinessRuleException("IMPORT_FILE_SIZE", $"File rỗng hoặc lớn hơn {CrudExcel.MaxFileBytes / 1024 / 1024} MB.");
            await using var content = file.OpenReadStream();
            return await r.ImportExcelAsync(content, ImportDefaults(form), ct);
        }).DisableAntiforgery();
        items.MapPost("/ImportXml", [Permission("DIGITAL_ITEMS", "add")] async (HttpRequest http, EbookItemResource r, CancellationToken ct) =>
        {
            var form = await http.ReadFormAsync(ct);
            if (form.Files.Any(f => f.Length is 0 or > EbookItemResource.MaxImportXmlBytes))
                throw new BusinessRuleException("IMPORT_FILE_SIZE", $"Mỗi file XML phải khác rỗng và không quá {EbookItemResource.MaxImportXmlBytes / 1000} KB.");
            var files = new List<(string, Stream)>();
            try
            {
                foreach (var f in form.Files.Where(f => f.Name == "file")) files.Add((f.FileName, f.OpenReadStream()));
                return await r.ImportXmlAsync(files, ImportDefaults(form), ct);
            }
            finally
            {
                foreach (var (_, stream) in files) await stream.DisposeAsync();
            }
        }).DisableAntiforgery();

        // Công cụ nhập liệu (LRC: tra ISBN, "Gợi ý AI") và khả năng tích hợp đang bật để giao diện ẩn nút của dịch vụ chưa cấu hình.
        items.MapGet("/Capabilities", [Permission("DIGITAL_ITEMS", "view")] (EbookItemTools t) => t.Capabilities());
        items.MapGet("/LookupIsbn", [PermissionAny("DIGITAL_ITEMS:add", "DIGITAL_ITEMS:edit")] (string? isbn, EbookItemTools t, CancellationToken ct)
            => t.LookupIsbnAsync(isbn, ct));
        items.MapPost("/SuggestClassification", [PermissionAny("DIGITAL_ITEMS:add", "DIGITAL_ITEMS:edit")] (SuggestClassificationRequest request, EbookItemTools t, CancellationToken ct)
            => t.SuggestAsync(request, ct));

        // Đồng bộ nội dung (trích chữ PDF + vector hóa chạy nền) và trạng thái.
        items.MapPost("/SyncContent", [Permission("DIGITAL_ITEMS", "edit")] (ItemSelection selection, EbookItemResource r, CancellationToken ct)
            => r.RequestContentSyncAsync(selection, ct));
        // Cán bộ tìm trong nội dung: thấy cả tài liệu ẩn và mọi đoạn trích, và được dùng tìm theo ngữ nghĩa (cần Ollama).
        items.MapPost("/SearchContent", [Permission("DIGITAL_ITEMS", "view")] (ContentSearchRequest request, OpacContentSearch search, CancellationToken ct)
            => search.SearchAsync(request, staff: true, ct));
        items.MapPost("/ContentStatus", [Permission("DIGITAL_ITEMS", "view")] (ContentStatusRequest request, EbookItemResource r, CancellationToken ct)
            => r.ContentStatusAsync(request.PublicIds, ct));

        // Gói DSpace Simple Archive: xuất kèm file PDF, nhập gói ZIP (metadata + file). Gói có thể lớn nên nâng trần thân yêu cầu cho riêng hai đường này.
        items.MapPost("/ExportWithFiles", [Permission("DIGITAL_ITEMS", "view")] async (ItemExportRequest request, EbookItemResource r, TimeProvider clock, CancellationToken ct)
            => Results.File(await r.ExportDspaceWithFilesAsync(request, ct), "application/zip", $"dspace-saf-{clock.GetUtcNow():yyyyMMddHHmmss}.zip"));
        items.MapPost("/ImportDspace", [Permission("DIGITAL_ITEMS", "add")] async (HttpRequest http, EbookItemResource r, CancellationToken ct) =>
        {
            var form = await http.ReadFormAsync(ct);
            var file = form.Files.GetFile("file") ?? throw new BusinessRuleException("IMPORT_FILE_EMPTY", "Chưa chọn gói ZIP.");
            if (file.Length is 0 or > MaxDspaceUploadBytes)
                throw new BusinessRuleException("IMPORT_FILE_SIZE", $"Gói rỗng hoặc lớn hơn {MaxDspaceUploadBytes / 1024 / 1024} MB.");
            await using var content = file.OpenReadStream();
            return await r.ImportDspaceAsync(content, ImportDefaults(form), ct);
        }).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(MaxDspaceUploadBytes), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = MaxDspaceUploadBytes });

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

        // Duyệt tài liệu bạn đọc nộp — thủ thư chỉ tra cứu + Duyệt/Từ chối, không Add/Update trực tiếp (bạn đọc tự nộp qua OPAC).
        var submissions = api.MapGroup("/document-submissions").WithTags("DocumentSubmissions");
        submissions.MapPost("/Search", [Permission("DIGITAL_SUBMISSIONS", "view")] (DocumentSubmissionSearch search, DocumentSubmissionResource r, CancellationToken ct)
            => r.SearchAsync(search, ct));
        submissions.MapGet("/GetById/{publicId:guid}", [Permission("DIGITAL_SUBMISSIONS", "view")] (Guid publicId, DocumentSubmissionResource r, CancellationToken ct)
            => r.GetAsync(publicId, ct));
        submissions.MapPost("/{publicId:guid}/Approve", [Permission("DIGITAL_SUBMISSIONS", "edit")] (Guid publicId, DocumentSubmissionResource r, ICurrentActor actor, CancellationToken ct)
            => r.ApproveAsync(publicId, actor.Id, ct));
        submissions.MapPost("/Reject", [Permission("DIGITAL_SUBMISSIONS", "edit")] (RejectSubmissionRequest request, DocumentSubmissionResource r, ICurrentActor actor, CancellationToken ct)
            => r.RejectAsync(request, actor.Id, ct));

        // Nội bộ: search dựng chỉ mục lần đầu / dựng lại — trạng thái mọi tài liệu theo Id tăng dần (không qua gateway, chỉ service token).
        app.MapGet("/internal/tenants/{tenantId:long}/ebooks", async (long tenantId, long? after, int? take, ITenantContext tenant, EbookStatePublisher states, CancellationToken ct) =>
        {
            var size = Math.Clamp(take ?? 300, 1, EbookStatePublisher.MaxStatePage);
            using (tenant.Use(tenantId))
            {
                var items = await states.StatesAsync(after ?? 0, size, ct);
                return new StatePage<EbookChanged>(items, items.Count == size ? items[^1].EbookId : null);
            }
        }).WithTags("Internal").RequireAuthorization(new RequireServiceCallerAttribute());

        return app;
    }
}
