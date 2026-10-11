using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Search.Application;
using Elib.Search.Domain;
using Elib.Search.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Elib.Search.Api;

/// <summary>
/// Tra cứu (gateway: /api/opac/search/** → /api/opac/** công khai theo host đơn vị; /api/admin/search/** → /api/** quản trị; cả hai cần
/// license SEARCH ở gateway).
/// </summary>
public static class SearchEndpoints
{
    public const string Module = "SEARCH";

    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var opac = app.MapGroup("/api/opac").WithTags("Opac").AllowAnonymous();
        opac.MapPost("/bibs/Search", async (OpacSearchRequest request, OpacSearch s, SearchStats stats, CancellationToken ct) =>
        {
            var result = await s.SearchAsync(request, ct);
            return result with { QueryId = await stats.RecordAsync(request, result.Total, ct) };
        });
        // Tra cứu liên thư viện trên OPAC: chỉ máy chủ quản trị bật "hiện trên OPAC" (gateway: policy opac-heavy — mỗi lượt mở kết nối ra ngoài).
        opac.MapGet("/z3950/servers", (Z3950Search z, CancellationToken ct) => z.ServersAsync(opacOnly: true, ct));
        opac.MapPost("/z3950/Search", (Z3950SearchRequest request, Z3950Search z, CancellationToken ct) => z.SearchAsync(request, opacOnly: true, ct));
        opac.MapPost("/stats/click", async (OpacClickRequest request, SearchStats stats, CancellationToken ct) =>
        {
            await stats.RecordClickAsync(request, ct);
            return Results.NoContent();
        });
        opac.MapGet("/bibs/{publicId:guid}", (Guid publicId, OpacSearch s, CancellationToken ct) => s.DetailAsync(publicId, ct));
        opac.MapGet("/bibs/{publicId:guid}/similar", (Guid publicId, int? size, OpacSearch s, CancellationToken ct) => s.SimilarAsync(publicId, size ?? 8, ct));
        opac.MapGet("/suggest", (string? q, OpacSearch s, CancellationToken ct) => s.SuggestAsync(q, ct));

        // Quản trị chỉ mục: trạng thái, dựng lại (chạy nền — đơn vị nhiều biểu ghi mất vài phút).
        var index = app.MapGroup("/api/index").WithTags("Index").RequireAuthorization(new RequiresModuleAttribute(Module));
        index.MapGet("/Status", [Permission("SEARCH_INDEX", "view")] (IndexRebuilder r, CancellationToken ct) => r.StatusAsync(ct));
        index.MapPost("/Rebuild", [Permission("SEARCH_INDEX", "edit")] async (ISearchDb db, ITenantContext tenant, IndexRebuildRunner runner,
            TimeProvider clock, CancellationToken ct) =>
        {
            var running = await db.Set<SearchSyncState>().AsNoTracking()
                .AnyAsync(s => s.Status == SearchSyncState.Running && s.StartedAt > clock.GetUtcNow().AddMinutes(-30), ct);
            if (running) throw new ConflictException("INDEX_REBUILDING", "Chỉ mục đang được dựng lại — chờ xong rồi thử lại.");
            _ = runner.Start(tenant.RequireTenantId());
            return Results.Accepted(value: new { Status = SearchSyncState.Running });
        });

        // Tra cứu liên thư viện (Z39.50/SRU): cấu hình máy chủ (Z3950_CONFIGS như monolith) và tra để sao biểu ghi về biên mục.
        var admin = app.MapGroup("/api").RequireAuthorization(new RequiresModuleAttribute(Module));
        var servers = admin.MapCrud<Z3950ServerResource>("/z3950-servers", "Z3950_CONFIGS").WithTags("Z3950");
        servers.MapPost("/{publicId:guid}/Test", [Permission("Z3950_CONFIGS", "view")] (Guid publicId, Z3950Search z, CancellationToken ct) => z.TestAsync(publicId, ct));
        var z3950 = admin.MapGroup("/z3950").WithTags("Z3950");
        z3950.MapGet("/servers", [PermissionAny("Z3950_CONFIGS:view", "CATALOG_BIBS:add", "CATALOG_BIBS:edit")] (Z3950Search z, CancellationToken ct)
            => z.ServersAsync(opacOnly: false, ct));
        z3950.MapPost("/Search", [PermissionAny("Z3950_CONFIGS:view", "CATALOG_BIBS:add", "CATALOG_BIBS:edit")] (Z3950SearchRequest request, Z3950Search z,
            CancellationToken ct) => z.SearchAsync(request, opacOnly: false, ct));

        // Thống kê chất lượng tìm kiếm OPAC.
        app.MapGet("/api/stats/Summary", [Permission("SEARCH_STATS", "view")] (DateOnly? from, DateOnly? to, SearchStats stats, CancellationToken ct)
            => stats.SummaryAsync(from, to, ct)).WithTags("Stats").RequireAuthorization(new RequiresModuleAttribute(Module));
        return app;
    }
}
