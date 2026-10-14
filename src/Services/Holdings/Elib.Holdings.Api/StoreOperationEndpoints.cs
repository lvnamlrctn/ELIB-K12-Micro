using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Holdings.Application;
using Elib.Holdings.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Api;

/// <summary>
/// Nghiệp vụ kho ngoài đăng ký cá biệt (gateway: /api/admin/holdings/**). Mã quyền giữ như monolith: INVENTORY, LOST_BOOKS, LIQUIDATES,
/// BOOK_OUT_STORE (+ danh mục EXPORT_REASON, BOOK_OUT_UNIT, EXHIBITION_LOCATION), AB_MOVES, AB_DELIVERERS, RE_REGISTER, STAT_DOC,
/// STORE_BOOK_REPORT. File Excel trả thẳng .xlsx.
/// </summary>
public static class StoreOperationEndpoints
{
    public static RouteGroupBuilder MapStoreOperationEndpoints(this RouteGroupBuilder api)
    {
        // Ô chọn kho của mọi màn kho — cán bộ không có quyền STORES vẫn chọn được kho.
        api.MapGet("/stores/Options", [PermissionAny(
            "STORES:view", "DOC_SEARCH:view", "MAP_SHELVING:view", "CATALOG_BIBS:view", "INVENTORY:view", "LOST_BOOKS:view", "LIQUIDATES:view",
            "BOOK_OUT_STORE:view", "AB_MOVES:view", "AB_DELIVERERS:view", "RE_REGISTER:view", "STAT_DOC:view", "STORE_BOOK_REPORT:view")]
            async (ICrudDbContext db, CancellationToken ct) =>
                await db.Set<Store>().AsNoTracking().OrderBy(s => s.Code).Select(s => new { s.Id, s.Code, s.Name }).ToListAsync(ct))
            .WithTags("Stores");

        MapLostBooks(api.MapGroup("/lost-books").WithTags("LostBooks"));
        MapLiquidations(api.MapGroup("/liquidations").WithTags("Liquidations"));
        MapStoreOuts(api);
        MapInventories(api.MapCrud<InventoryResource>("/inventories", "INVENTORY").WithTags("Inventories"));
        MapMoves(api.MapCrud<StoreMoveResource>("/store-moves", "AB_MOVES").WithTags("StoreMoves"));
        MapDeliveries(api.MapCrud<DeliveryResource>("/deliveries", "AB_DELIVERERS").WithTags("Deliveries"));

        var reRegister = api.MapGroup("/items/ReRegister").WithTags("ReRegister");
        reRegister.MapPost("", [Permission("RE_REGISTER", "edit")] (ReRegisterRequest request, BarcodeReRegistration r, CancellationToken ct)
            => r.ReRegisterAsync(request, ct));
        reRegister.MapGet("/Template", [Permission("RE_REGISTER", "edit")] ()
            => Results.File(BarcodeReRegistration.Template(), CrudExcel.ContentType, "Mau dang ky lai ma vach.xlsx"));
        reRegister.MapPost("/Import", [Permission("RE_REGISTER", "edit")] async (IFormFile file, bool? dryRun, BarcodeReRegistration r, CancellationToken ct) =>
        {
            using var buffer = await BufferAsync(file, ct);
            var result = await r.ImportAsync(buffer, dryRun ?? false, ct);
            return result.ErrorCount > 0 ? Results.Json(result, statusCode: StatusCodes.Status400BadRequest) : Results.Ok(result);
        }).DisableAntiforgery();

        var stats = api.MapGroup("/statistics").WithTags("Statistics");
        stats.MapPost("/Document", [Permission("STAT_DOC", "view")] (DocumentStatsRequest request, StoreReports r, CancellationToken ct)
            => r.DocumentAsync(request, ct));
        stats.MapPost("/DocumentExport", [Permission("STAT_DOC", "view")] async (DocumentStatsRequest request, StoreReports r, CancellationToken ct)
            => File(await r.DocumentExportAsync(request, ct)));

        var report = api.MapGroup("/store-book-report").WithTags("StoreBookReport");
        report.MapPost("/Search", [Permission("STORE_BOOK_REPORT", "view")] (StoreBookReportRequest request, StoreReports r, CancellationToken ct)
            => r.BookReportAsync(request, ct));
        report.MapPost("/Export", [Permission("STORE_BOOK_REPORT", "view")] async (StoreBookReportRequest request, StoreReports r, CancellationToken ct)
            => File(await r.BookReportExportAsync(request, ct)));
        return api;
    }

    private static void MapLostBooks(RouteGroupBuilder g)
    {
        g.MapPost("/Search", [Permission("LOST_BOOKS", "view")] (StoreRecordSearch search, LostBooks r, CancellationToken ct) => r.SearchAsync(search, ct));
        g.MapPost("/Export", [Permission("LOST_BOOKS", "view")] async (StoreRecordSearch search, LostBooks r, CancellationToken ct)
            => File(await r.ExportAsync(search, ct)));
        g.MapPost("/MarkLost", [Permission("LOST_BOOKS", "add")] (MarkLostRequest request, LostBooks r, CancellationToken ct) => r.MarkLostAsync(request, ct));
        g.MapPost("/Restore", [Permission("LOST_BOOKS", "edit")] async (BarcodeRequest request, LostBooks r, CancellationToken ct) =>
        {
            await r.RestoreAsync(request, ct);
            return Results.NoContent();
        });
    }

    private static void MapLiquidations(RouteGroupBuilder g)
    {
        g.MapPost("/Search", [Permission("LIQUIDATES", "view")] (StoreRecordSearch search, Liquidations r, CancellationToken ct) => r.SearchAsync(search, ct));
        g.MapPost("/Export", [Permission("LIQUIDATES", "view")] async (StoreRecordSearch search, Liquidations r, CancellationToken ct)
            => File(await r.ExportAsync(search, ct)));
        g.MapPost("/Liquidate", [Permission("LIQUIDATES", "add")] (LiquidateRequest request, Liquidations r, CancellationToken ct)
            => r.LiquidateAsync(request, ct));
        g.MapPost("/Cancel", [Permission("LIQUIDATES", "edit")] async (BarcodeRequest request, Liquidations r, CancellationToken ct) =>
        {
            await r.CancelAsync(request, ct);
            return Results.NoContent();
        });
    }

    private static void MapStoreOuts(RouteGroupBuilder api)
    {
        api.MapCrud<ExportReasonResource>("/export-reasons", "EXPORT_REASON").WithTags("ExportReasons");
        api.MapCrud<BookOutUnitResource>("/book-out-units", "BOOK_OUT_UNIT").WithTags("BookOutUnits");
        api.MapCrud<ExhibitionLocationResource>("/exhibition-locations", "EXHIBITION_LOCATION").WithTags("ExhibitionLocations");

        var g = api.MapGroup("/store-outs").WithTags("StoreOuts");
        // Ô chọn của màn quét: cán bộ chỉ có quyền BOOK_OUT_STORE vẫn xem được 3 danh mục.
        g.MapGet("/Options", [Permission("BOOK_OUT_STORE", "view")] async (ICrudDbContext db, CancellationToken ct) => new
        {
            Reasons = await db.Set<ExportReason>().AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToListAsync(ct),
            Units = await db.Set<BookOutUnit>().AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToListAsync(ct),
            Locations = await db.Set<ExhibitionLocation>().AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToListAsync(ct),
        });
        g.MapPost("/Search", [Permission("BOOK_OUT_STORE", "view")] (StoreOutSearch search, StoreOuts r, CancellationToken ct) => r.SearchAsync(search, ct));
        g.MapPost("/Export", [Permission("BOOK_OUT_STORE", "view")] async (StoreOutSearch search, StoreOuts r, CancellationToken ct)
            => File(await r.ExportAsync(search, ct)));
        g.MapPost("/ScanOut", [Permission("BOOK_OUT_STORE", "edit")] (ScanOutRequest request, StoreOuts r, CancellationToken ct) => r.ScanOutAsync(request, ct));
        g.MapPost("/ScanIn", [Permission("BOOK_OUT_STORE", "edit")] (BarcodeRequest request, StoreOuts r, CancellationToken ct) => r.ScanInAsync(request, ct));
        g.MapDelete("/Delete/{publicId:guid}", [Permission("BOOK_OUT_STORE", "delete")] async (Guid publicId, StoreOuts r, CancellationToken ct) =>
        {
            await r.DeleteAsync(publicId, ct);
            return Results.NoContent();
        });
    }

    private static void MapInventories(RouteGroupBuilder g)
    {
        g.MapPost("/Close/{publicId:guid}", [Permission("INVENTORY", "edit")] ([FromServices] InventoryResource r, Guid publicId, CancellationToken ct)
            => r.CloseAsync(publicId, ct));
        g.MapPost("/Reopen/{publicId:guid}", [Permission("INVENTORY", "edit")] ([FromServices] InventoryResource r, Guid publicId, CancellationToken ct)
            => r.ReopenAsync(publicId, ct));
        g.MapPost("/Scan", [Permission("INVENTORY", "add")] ([FromServices] InventoryResource r, ScanRequest request, CancellationToken ct)
            => r.ScanAsync(request, ct));
        g.MapGet("/ScanTemplate", [Permission("INVENTORY", "add")] ()
            => Results.File(InventoryResource.ScanTemplate(), CrudExcel.ContentType, "Mau nhap kiem ke.xlsx"));
        g.MapPost("/ImportScans/{publicId:guid}", [Permission("INVENTORY", "add")] async ([FromServices] InventoryResource r, Guid publicId, IFormFile file,
            CancellationToken ct) =>
        {
            using var buffer = await BufferAsync(file, ct);
            var result = await r.ImportAsync(publicId, buffer, ct);
            return result.Errors.Count > 0 ? Results.Json(result, statusCode: StatusCodes.Status400BadRequest) : Results.Ok(result);
        }).DisableAntiforgery();
        g.MapPost("/Scans", [Permission("INVENTORY", "view")] ([FromServices] InventoryResource r, ScanSearch search, CancellationToken ct)
            => r.ScansAsync(search, ct));
        g.MapGet("/Summary/{publicId:guid}", [Permission("INVENTORY", "view")] ([FromServices] InventoryResource r, Guid publicId, CancellationToken ct)
            => r.SummaryAsync(publicId, ct));
        g.MapGet("/Missing/{publicId:guid}", [Permission("INVENTORY", "view")] ([FromServices] InventoryResource r, Guid publicId, int? pageIndex, int? pageSize,
            CancellationToken ct) => r.MissingAsync(publicId, pageIndex ?? 1, pageSize ?? 20, ct));
        g.MapGet("/Report/{publicId:guid}", [Permission("INVENTORY", "view")] async ([FromServices] InventoryResource r, Guid publicId, CancellationToken ct)
            => File(await r.ReportAsync(publicId, ct)));
    }

    private static void MapMoves(RouteGroupBuilder g)
    {
        g.MapGet("/Lines/{publicId:guid}", [Permission("AB_MOVES", "view")] ([FromServices] StoreMoveResource r, Guid publicId, CancellationToken ct)
            => r.LinesAsync(publicId, ct));
        g.MapPost("/Candidates", [Permission("AB_MOVES", "view")] ([FromServices] StoreMoveResource r, TransferCandidateSearch search, CancellationToken ct)
            => r.CandidatesAsync(search, ct));
        g.MapPost("/AddLines/{publicId:guid}", [Permission("AB_MOVES", "edit")] ([FromServices] StoreMoveResource r, Guid publicId, AddLinesRequest request,
            CancellationToken ct) => r.AddLinesAsync(publicId, request, ct));
        g.MapDelete("/Lines/{linePublicId:guid}", [Permission("AB_MOVES", "edit")] async ([FromServices] StoreMoveResource r, Guid linePublicId, CancellationToken ct) =>
        {
            await r.RemoveLineAsync(linePublicId, ct);
            return Results.NoContent();
        });
        g.MapPost("/Complete/{publicId:guid}", [Permission("AB_MOVES", "edit")] ([FromServices] StoreMoveResource r, Guid publicId, CancellationToken ct)
            => r.CompleteAsync(publicId, ct));
    }

    private static void MapDeliveries(RouteGroupBuilder g)
    {
        g.MapGet("/Statuses", () => DeliveryStatus.Names.Select(s => new { code = s.Key, name = s.Value }));
        g.MapGet("/Lines/{publicId:guid}", [Permission("AB_DELIVERERS", "view")] ([FromServices] DeliveryResource r, Guid publicId, CancellationToken ct)
            => r.LinesAsync(publicId, ct));
        g.MapPost("/Candidates", [Permission("AB_DELIVERERS", "view")] ([FromServices] DeliveryResource r, TransferCandidateSearch search, CancellationToken ct)
            => r.CandidatesAsync(search, ct));
        g.MapPost("/AddLines/{publicId:guid}", [Permission("AB_DELIVERERS", "edit")] ([FromServices] DeliveryResource r, Guid publicId, AddLinesRequest request,
            CancellationToken ct) => r.AddLinesAsync(publicId, request, ct));
        g.MapDelete("/Lines/{linePublicId:guid}", [Permission("AB_DELIVERERS", "edit")] async ([FromServices] DeliveryResource r, Guid linePublicId, CancellationToken ct) =>
        {
            await r.RemoveLineAsync(linePublicId, ct);
            return Results.NoContent();
        });
        g.MapPost("/Sign/{publicId:guid}", [Permission("AB_DELIVERERS", "edit")] ([FromServices] DeliveryResource r, Guid publicId, CancellationToken ct)
            => r.SignAsync(publicId, ct));
    }

    private static IResult File(ExcelFile file) => Results.File(file.Content, CrudExcel.ContentType, file.FileName);

    private static async Task<MemoryStream> BufferAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length is 0 or > CrudExcel.MaxFileBytes)
            throw new BusinessRuleException("IMPORT_FILE_SIZE", $"File rỗng hoặc lớn hơn {CrudExcel.MaxFileBytes / 1024 / 1024} MB.");
        var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        return buffer;
    }
}
