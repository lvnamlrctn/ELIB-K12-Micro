using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.Patron.Application;

namespace Elib.Patron.Api;

/// <summary>
/// Bạn đọc và tham số bạn đọc (gateway: /api/admin/patron/** → /api/**). Mã quyền giữ như monolith:
/// READERS, READER_TYPES, CLASSES, COURSES, GROUPREADER.
/// </summary>
public static class PatronEndpoints
{
    public static IEndpointRouteBuilder MapPatronEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCrud<ReaderTypeResource>("/api/reader-types", "READER_TYPES").WithTags("ReaderTypes");
        app.MapCrud<SchoolClassResource>("/api/classes", "CLASSES").WithTags("Classes");
        app.MapCrud<CourseResource>("/api/courses", "COURSES").WithTags("Courses");
        app.MapCrud<ReaderGroupResource>("/api/reader-groups", "GROUPREADER").WithTags("ReaderGroups");

        var readers = app.MapCrud<ReaderResource>("/api/readers", "READERS").WithTags("Readers");
        readers.MapGet("/CheckExist", [Permission("READERS", "view")] (string cardNo, Guid? excludePublicId, ReaderResource r, CancellationToken ct)
            => r.CheckExistAsync(cardNo, excludePublicId, ct));
        readers.MapPost("/Lock/{publicId:guid}", [Permission("READERS", "edit")] (Guid publicId, LockReaderRequest? request, ReaderResource r, CancellationToken ct)
            => r.LockAsync(publicId, request?.Reason, ct));
        readers.MapPost("/Unlock/{publicId:guid}", [Permission("READERS", "edit")] (Guid publicId, ReaderResource r, CancellationToken ct)
            => r.UnlockAsync(publicId, ct));
        readers.MapPut("/BulkUpdate", [Permission("READERS", "edit")] async (ReaderBulkUpdateRequest request, ReaderResource r, CancellationToken ct)
            => Results.Ok(new { updatedCount = await r.BulkUpdateAsync(request, ct) }));
        readers.MapPut("/Photo/{publicId:guid}", [Permission("READERS", "edit")] (Guid publicId, ReaderPhotoRequest request, ReaderResource r, CancellationToken ct)
            => r.SetPhotoAsync(publicId, request.FileId, ct));
        readers.MapPost("/Photos", [Permission("READERS", "edit")] (ReaderPhotosRequest request, ReaderResource r, CancellationToken ct)
            => r.SetPhotosAsync(request, ct));
        readers.MapGet("/GetExportFields", [Permission("READERS", "view")] () => ReaderResource.ExportFields);
        readers.MapPost("/Export", [Permission("READERS", "view")] async (ReaderExportRequest request, ReaderResource r, CancellationToken ct)
            => Results.File(await r.ExportAsync(request, ct), CrudExcel.ContentType, "danh-sach-ban-doc.xlsx"));
        return app;
    }
}
