using Elib.BuildingBlocks.Authorization;
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
        opac.MapPost("/bibs/Search", (OpacSearchRequest request, OpacSearch s, CancellationToken ct) => s.SearchAsync(request, ct));
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
        return app;
    }
}
