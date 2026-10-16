using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using System.Text.Json;
using Elib.BuildingBlocks.Domain;
using Elib.Patron.Application;
using Elib.Patron.Domain;
using Microsoft.AspNetCore.Mvc;

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
        // Thao tác hàng loạt kiểu ELIB-LRC: theo danh sách đã chọn hoặc theo bộ lọc (xem trước → mã xác nhận 15 phút).
        readers.MapPost("/Batch/Preview", [Permission("READERS", "edit")] (ReaderSearch search, ReaderResource r, CancellationToken ct)
            => r.PreviewSelectionAsync(search, ct));
        readers.MapPut("/BatchUpdate", [Permission("READERS", "edit")] (ReaderBatchUpdateRequest request, ReaderResource r, CancellationToken ct)
            => r.BatchUpdateAsync(request, ct));
        readers.MapGet("/Batch/Jobs/{id:guid}", [Permission("READERS", "edit")] (Guid id, ReaderImportService s, CancellationToken ct)
            => s.GetBatchAsync(id, ct));
        readers.MapPost("/BulkDelete", [Permission("READERS", "delete")] (ReaderBulkDeleteRequest request, ReaderResource r, CancellationToken ct)
            => r.BulkDeleteAsync(request, ct));
        readers.MapPut("/Groups", [Permission("READERS", "edit")] (ReaderGroupsRequest request, ReaderResource r, CancellationToken ct)
            => r.ChangeGroupsAsync(request, ct));
        readers.MapGet("/Groups/{publicId:guid}", [Permission("READERS", "view")] (Guid publicId, ReaderResource r, CancellationToken ct)
            => r.GroupsOfAsync(publicId, ct));
        readers.MapPut("/Groups/{publicId:guid}", [Permission("READERS", "edit")] (Guid publicId, ReaderGroupSetRequest request, ReaderResource r,
            CancellationToken ct) => r.SetGroupsAsync(publicId, request, ct));
        readers.MapPut("/Photo/{publicId:guid}", [Permission("READERS", "edit")] (Guid publicId, ReaderPhotoRequest request, ReaderResource r, CancellationToken ct)
            => r.SetPhotoAsync(publicId, request.FileId, ct));
        // Nhận diện khuôn mặt tại quầy mượn trả: chỉ nhận diện (không ghi gì), nên cán bộ xem bạn đọc hoặc mượn trả đều gọi được.
        readers.MapGet("/FaceStatus", [PermissionAny("READERS:view", "BORROW:view", "BORROW:add")] (FaceRecognition f) => f.Status());
        readers.MapPost("/FaceIdentify", [PermissionAny("READERS:view", "BORROW:view", "BORROW:add")] (FaceIdentifyRequest request, FaceRecognition f, CancellationToken ct)
            => f.IdentifyAsync(request.ImageBase64, ct));
        // Ảnh khuôn mặt bổ sung (tối đa ReaderPhoto.MaxPerReader mỗi bạn đọc): dùng cùng ảnh thẻ khi nhận diện khuôn mặt.
        readers.MapGet("/ExtraPhotos/{publicId:guid}", [Permission("READERS", "view")] (Guid publicId, ReaderResource r, CancellationToken ct)
            => r.ExtraPhotosAsync(publicId, ct));
        readers.MapPost("/ExtraPhotos/{publicId:guid}", [Permission("READERS", "edit")] (Guid publicId, ReaderExtraPhotoRequest request, ReaderResource r, CancellationToken ct)
            => r.AddExtraPhotoAsync(publicId, request.FileId, ct));
        readers.MapDelete("/ExtraPhotos/{publicId:guid}/{fileId:guid}", [Permission("READERS", "edit")] async (Guid publicId, Guid fileId, ReaderResource r, CancellationToken ct) =>
        {
            await r.RemoveExtraPhotoAsync(publicId, fileId, ct);
            return Results.NoContent();
        });
        readers.MapPost("/Photos", [Permission("READERS", "edit")] (ReaderPhotosRequest request, ReaderResource r, CancellationToken ct)
            => r.SetPhotosAsync(request, ct));
        // Nội bộ: service khác lấy trạng thái bạn đọc khi bản sao chưa có (không qua gateway, chỉ service token).
        app.MapGet("/internal/tenants/{tenantId:long}/readers/by-card/{cardNo}", async (long tenantId, string cardNo, ITenantContext tenant, ReaderResource r, CancellationToken ct) =>
        {
            using (tenant.Use(tenantId))
                return await r.CurrentStateAsync(cardNo, ct) is { } state ? Results.Ok(state) : Results.NotFound();
        }).WithTags("Internal").RequireAuthorization(new RequireServiceCallerAttribute());
        app.MapGet("/internal/tenants/{tenantId:long}/readers/{publicId:guid}", async (long tenantId, Guid publicId, ITenantContext tenant, ReaderResource r, CancellationToken ct) =>
        {
            using (tenant.Use(tenantId))
                return await r.CurrentStateAsync(publicId, ct) is { } state ? Results.Ok(state) : Results.NotFound();
        }).WithTags("Internal").RequireAuthorization(new RequireServiceCallerAttribute());
        // Nhập Excel nâng cao (ELIB-LRC: Import/Columns + Import với mapping/overwrite/preview/background): đọc cột → xem trước → chạy nền.
        readers.MapPost("/Import/Columns", [Permission("READERS", "add")] async (IFormFile file, ReaderImportService s, CancellationToken ct) =>
            s.Inspect(await ReadFileAsync(file, ct))).DisableAntiforgery();
        readers.MapPost("/Import/Jobs", [Permission("READERS", "add")] async (IFormFile file, [FromForm] string options, ReaderImportService s,
            CancellationToken ct) =>
        {
            ReaderImportOptions parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<ReaderImportOptions>(options, ReaderImportService.Json)
                    ?? throw new BusinessRuleException("IMPORT_OPTIONS_INVALID", "Thiếu tuỳ chọn nhập.");
            }
            catch (JsonException)
            {
                throw new BusinessRuleException("IMPORT_OPTIONS_INVALID", "Tuỳ chọn nhập không hợp lệ.");
            }
            return await s.PreviewAsync(file.FileName, (await ReadFileAsync(file, ct)).ToArray(), parsed, ct);
        }).DisableAntiforgery();
        readers.MapGet("/Import/Jobs/{id:guid}", [Permission("READERS", "add")] (Guid id, ReaderImportService s, CancellationToken ct) => s.GetAsync(id, ct));
        readers.MapPost("/Import/Jobs/{id:guid}/Start", [Permission("READERS", "add")] (Guid id, ReaderImportService s, CancellationToken ct) => s.StartAsync(id, ct));
        readers.MapGet("/Import/Jobs/{id:guid}/Errors", [Permission("READERS", "add")] async (Guid id, ReaderImportService s, CancellationToken ct) =>
        {
            var (bytes, name) = await s.ErrorFileAsync(id, ct);
            return Results.File(bytes, CrudExcel.ContentType, name);
        });
        readers.MapGet("/GetExportFields", [Permission("READERS", "view")] () => ReaderResource.ExportFields);
        readers.MapPost("/Export", [Permission("READERS", "view")] async (ReaderExportRequest request, ReaderResource r, CancellationToken ct)
            => Results.File(await r.ExportAsync(request, ct), CrudExcel.ContentType, "danh-sach-ban-doc.xlsx"));
        return app;
    }

    private static async Task<MemoryStream> ReadFileAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length is 0 or > ReaderImportJob.MaxFileBytes)
            throw new BusinessRuleException("IMPORT_FILE_SIZE", $"File rỗng hoặc lớn hơn {ReaderImportJob.MaxFileBytes / 1024 / 1024} MB.");
        var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        return buffer;
    }
}
