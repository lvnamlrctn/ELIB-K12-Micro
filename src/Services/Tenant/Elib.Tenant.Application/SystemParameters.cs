using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Platform;
using Elib.Tenant.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Elib.Tenant.Application;

public sealed class SystemParameterSearch : CrudSearch
{
    public string? Service { get; set; }
}

public sealed record SystemParameterRequest(
    string Code, string? Value, string? Description, string? DescriptionEn, string? Type, string? Service, bool IsPublic = false);

public sealed record SystemParameterDto(
    long Id, Guid PublicId, string Code, string? Value, string? Description, string? DescriptionEn, string? Type, string? Service,
    bool IsPublic, bool IsBuiltIn);

/// <summary>Giá trị tham số cho OPAC (chỉ tham số công khai).</summary>
public sealed record PublicParameterDto(string Code, string? Value, string? Description);

/// <summary>
/// Tham số hệ thống (monolith: SystemParameterController). Mỗi lần ghi phát <see cref="SystemParameterChanged"/> qua outbox
/// để service đọc tham số xoá cache (docs 04 §5: tenant:{id}:params:{service}).
/// </summary>
public sealed class SystemParameterResource(ICrudDbContext db, IPublishEndpoint publisher, ITenantContext tenant)
    : CrudResource<SystemParameterResource, SystemParameter, SystemParameterSearch, SystemParameterRequest, SystemParameterDto>(db)
{
    protected override string EntityName => "Tham số";

    protected override string? Describe(SystemParameter entity) => entity.Code;

    private static readonly string[] BuiltInCodes = ParameterCatalog.All.Select(d => d.Code).ToArray();

    // BuiltInCodes là mảng tĩnh — EF dịch Contains thành tham số IN (...).
    protected override Expression<Func<SystemParameter, SystemParameterDto>> Projection =>
        x => new SystemParameterDto(x.Id, x.PublicId, x.Code, x.Value, x.Description, x.DescriptionEn, x.Type, x.Service, x.IsPublic,
            BuiltInCodes.Contains(x.Code));

    protected override SystemParameter Create(SystemParameterRequest request) =>
        SystemParameter.Create(request.Code, request.Value, request.Description, request.DescriptionEn, request.Type, request.Service, request.IsPublic);

    protected override void Update(SystemParameter entity, SystemParameterRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Code) && SystemParameter.NormalizeCode(request.Code) != entity.Code)
            throw new BusinessRuleException("PARAMETER_CODE_IMMUTABLE", "Không đổi được mã tham số — xoá rồi thêm mã mới.");
        entity.Update(request.Value, request.Description, request.DescriptionEn, request.Type, request.Service, request.IsPublic);
    }

    protected override IQueryable<SystemParameter> Filter(IQueryable<SystemParameter> query, SystemParameterSearch search)
    {
        if (!string.IsNullOrWhiteSpace(search.Service))
        {
            var service = search.Service.Trim().ToLowerInvariant();
            query = query.Where(x => x.Service == service);
        }
        if (search.Term is not { } term) return query;
        var code = term.ToUpperInvariant();
#pragma warning disable CA1862, CA1304, CA1311
        return query.Where(x => x.Code.Contains(code) || (x.Description != null && x.Description.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<SystemParameter> Order(IQueryable<SystemParameter> query) => query.OrderBy(x => x.Code);

    protected override async Task ValidateAsync(SystemParameter entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Code == entity.Code && x.Id != entity.Id, ct))
            throw new ConflictException("PARAMETER_CODE_EXISTS", $"Tham số '{entity.Code}' đã có.");
    }

    protected override Task OnSavingAsync(SystemParameter entity, CrudChange change, CancellationToken ct) =>
        publisher.Publish(new SystemParameterChanged
        {
            TenantId = tenant.RequireTenantId(),
            Service = entity.Service ?? "*",
            Keys = [entity.Code],
        }, ct);
}

/// <summary>Đọc giá trị hiệu lực: bản ghi của đơn vị, thiếu thì lấy mặc định trong <see cref="ParameterCatalog"/>.</summary>
public sealed class ParameterQueries(ITenantDb db, ITenantContext tenant)
{
    /// <summary>OPAC — đơn vị lấy theo host (header gateway). Chỉ tham số công khai; tham số khác coi như không tồn tại.</summary>
    public async Task<IReadOnlyList<PublicParameterDto>> PublicAsync(IEnumerable<string> codes, CancellationToken ct)
    {
        tenant.RequireTenantId();
        var wanted = codes.Select(c => (c ?? "").Trim().ToUpperInvariant()).Where(c => c.Length > 0).Distinct().Take(50).ToList();
        var rows = await db.SystemParameters.AsNoTracking().Where(p => wanted.Contains(p.Code)).ToListAsync(ct);

        var result = new List<PublicParameterDto>();
        foreach (var code in wanted)
        {
            var row = rows.FirstOrDefault(r => r.Code == code);
            if (row is not null)
            {
                if (row.IsPublic) result.Add(new PublicParameterDto(row.Code, row.Value, row.Description));
            }
            else if (ParameterCatalog.Find(code) is { IsPublic: true } d)
            {
                result.Add(new PublicParameterDto(d.Code, d.DefaultValue, d.Description));
            }
        }
        return result;
    }

    /// <summary>Service khác đọc tham số của một đơn vị (bỏ trống <paramref name="service"/> = tất cả).</summary>
    public async Task<IReadOnlyDictionary<string, string?>> EffectiveAsync(long tenantId, string? service, CancellationToken ct)
    {
        var svc = string.IsNullOrWhiteSpace(service) ? null : service.Trim().ToLowerInvariant();
        using var scope = tenant.Use(tenantId);
        var rows = await db.SystemParameters.AsNoTracking()
            .Where(p => svc == null || p.Service == svc)
            .Select(p => new { p.Code, p.Value })
            .ToListAsync(ct);

        var result = ParameterCatalog.All.Where(d => svc is null || d.Service == svc)
            .ToDictionary(d => d.Code, d => d.DefaultValue, StringComparer.Ordinal);
        foreach (var row in rows) result[row.Code] = row.Value;
        return result;
    }
}

/// <summary>
/// Chép danh mục mặc định cho đơn vị: tham số, quốc tịch, dân tộc, học vị, chức vụ, tiền tệ. Idempotent — chỉ thêm
/// mục còn thiếu (theo mã/tên, kể cả mục đã bị xoá mềm: đơn vị đã xoá thì không chép lại).
/// Chạy trong ngữ cảnh hệ thống (tạo đơn vị) nên TenantId phải đặt tường minh.
/// </summary>
public sealed class TenantDefaults(ITenantDb db)
{
    public async Task<int> SeedAsync(long tenantId, CancellationToken ct)
    {
        var added = 0;

        var codes = await db.SystemParameters.IgnoreQueryFilters().Where(p => p.TenantId == tenantId).Select(p => p.Code).ToListAsync(ct);
        foreach (var d in ParameterCatalog.All.Where(d => !codes.Contains(d.Code)))
        {
            var parameter = SystemParameter.FromDefinition(d);
            parameter.TenantId = tenantId;
            db.SystemParameters.Add(parameter);
            added++;
        }

        var currencies = await db.Currencies.IgnoreQueryFilters().Where(c => c.TenantId == tenantId).Select(c => c.Code).ToListAsync(ct);
        foreach (var (code, name, rate) in ReferenceDefaults.Currencies.Where(c => !currencies.Contains(c.Code)))
        {
            var currency = Currency.Create(code, name, rate, IHasStatus.Active);
            currency.TenantId = tenantId;
            db.Currencies.Add(currency);
            added++;
        }

        added += await SeedNamesAsync(db.Nationalities, tenantId, ReferenceDefaults.Nationalities, ct);
        added += await SeedNamesAsync(db.Ethnicities, tenantId, ReferenceDefaults.Ethnicities, ct);
        added += await SeedNamesAsync(db.Degrees, tenantId, ReferenceDefaults.Degrees, ct);
        added += await SeedNamesAsync(db.Positions, tenantId, ReferenceDefaults.Positions, ct);
        return added;
    }

    private static async Task<int> SeedNamesAsync<T>(DbSet<T> set, long tenantId, IReadOnlyList<string> names, CancellationToken ct)
        where T : NamedCatalogItem, new()
    {
        var existing = await set.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).Select(x => x.Name).ToListAsync(ct);
        var added = 0;
        foreach (var name in names.Where(n => !existing.Contains(n)))
        {
            var item = NamedCatalogItem.New<T>(name);
            item.TenantId = tenantId;
            set.Add(item);
            added++;
        }
        return added;
    }
}
