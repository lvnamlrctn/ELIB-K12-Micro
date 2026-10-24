using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Serials.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Serials.Application;

/// <summary>Loại ấn phẩm (monolith: MagazineTypeController, quyền MAGAZINE_TYPES).</summary>
public sealed class PublicationTypeResource(ICrudDbContext db) : NamedCatalogResource<PublicationTypeResource, PublicationType>(db)
{
    public static readonly IReadOnlyList<string> Defaults = ["Báo", "Tạp chí"];

    protected override string EntityName => "Loại ấn phẩm";

    protected override async Task EnsureDeletableAsync(PublicationType entity, CancellationToken ct)
    {
        if (await Db.Set<Subscription>().AnyAsync(s => s.PublicationTypeId == entity.Id, ct))
            throw new ConflictException("TYPE_IN_USE", $"Loại '{entity.Name}' đang có đăng ký, không xoá được.");
    }

    public async Task<int> AddMissingDefaultsAsync(CancellationToken ct)
    {
        var existing = await Set.Select(x => x.Name).ToListAsync(ct);
        var added = 0;
        foreach (var name in Defaults.Where(n => !existing.Contains(n)))
        {
            Set.Add(NamedCatalogItem.New<PublicationType>(name));
            added++;
        }
        await Db.SaveChangesAsync(ct);
        return added;
    }
}

public sealed record FrequencyDto(
    long Id, Guid PublicId, string Name, int Unit, string UnitName, int? IssuesPerUnit, int? UnitsPerIssue, int? ReleaseDay, int SortOrder, DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Tần suất xuất bản (monolith: FrequencyMagazineController, quyền FREQUENCIES).</summary>
public sealed class FrequencyResource(ICrudDbContext db) : CrudResource<FrequencyResource, Frequency, CrudSearch, FrequencyData, FrequencyDto>(db)
{
    /// <summary>Tần suất mặc định cho đơn vị mới: (tên, đơn vị, số kỳ mỗi đơn vị, số đơn vị mỗi kỳ).</summary>
    public static readonly IReadOnlyList<FrequencyData> Defaults =
    [
        new("Hằng ngày", FrequencyUnit.Day, 1, null, null, 1),
        new("Hằng tuần", FrequencyUnit.Week, 1, null, null, 2),
        new("Hai tuần một kỳ", FrequencyUnit.Week, null, 2, null, 3),
        new("Nửa tháng", FrequencyUnit.Month, 2, null, null, 4),
        new("Hằng tháng", FrequencyUnit.Month, 1, null, null, 5),
        new("Hằng quý", FrequencyUnit.Quarter, 1, null, null, 6),
        new("Hằng năm", FrequencyUnit.Year, 1, null, null, 7),
    ];

    protected override string EntityName => "Tần suất";

    protected override string? Describe(Frequency entity) => entity.Name;

    protected override Expression<Func<Frequency, FrequencyDto>> Projection => x => new FrequencyDto(
        x.Id, x.PublicId, x.Name, x.Unit, x.Unit == 1 ? "Ngày" : x.Unit == 2 ? "Tuần" : x.Unit == 3 ? "Tháng" : x.Unit == 4 ? "Quý" : "Năm",
        x.IssuesPerUnit, x.UnitsPerIssue, x.ReleaseDay, x.SortOrder, x.CreatedAt, x.UpdatedAt);

    protected override Frequency Create(FrequencyData request) => Frequency.Create(request);

    protected override void Update(Frequency entity, FrequencyData request) => entity.Update(request);

    protected override IQueryable<Frequency> Filter(IQueryable<Frequency> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<Frequency> Order(IQueryable<Frequency> query) => query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name);

    protected override async Task ValidateAsync(Frequency entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Name == entity.Name && x.Id != entity.Id, ct))
            throw new ConflictException("NAME_EXISTS", $"Tần suất '{entity.Name}' đã có.");
    }

    protected override async Task EnsureDeletableAsync(Frequency entity, CancellationToken ct)
    {
        if (await Db.Set<Subscription>().AnyAsync(s => s.FrequencyId == entity.Id, ct))
            throw new ConflictException("FREQUENCY_IN_USE", $"Tần suất '{entity.Name}' đang có đăng ký, không xoá được.");
    }

    public async Task<int> AddMissingDefaultsAsync(CancellationToken ct)
    {
        var existing = await Set.Select(x => x.Name).ToListAsync(ct);
        var added = 0;
        foreach (var data in Defaults.Where(d => !existing.Contains(d.Name)))
        {
            Set.Add(Frequency.Create(data));
            added++;
        }
        await Db.SaveChangesAsync(ct);
        return added;
    }
}

/// <summary>Mẫu đánh số kèm chi tiết X/Y/Z trong cùng DTO (không có endpoint chi tiết riêng như monolith).</summary>
public sealed record PatternDto(
    long Id, Guid PublicId, string Name, string? Description, string LabelTemplate, string? NameX, string? NameY, string? NameZ, int StepX, int StepY, int StepZ,
    int? MaxX, int? MaxY, int? MaxZ, int ResetX, int ResetY, int ResetZ, int SortOrder, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Mẫu đánh số (monolith: PatternMagazineController + PartemMagazineDetail, quyền PATTERNS).</summary>
public sealed class PatternResource(ICrudDbContext db) : CrudResource<PatternResource, NumberingPattern, CrudSearch, PatternData, PatternDto>(db)
{
    public static readonly IReadOnlyList<PatternData> Defaults =
    [
        new("Số thứ tự liên tục", "Số 1, 2, 3… không đặt lại", "Số {X}", "Số", SortOrder: 1),
        new("Số trong năm / năm", "Số 1/2026 … 12/2026, sang năm mới đặt lại", "Số {X}/{Y}", "Số", "Năm", MaxX: 12, SortOrder: 2),
        new("Số trong tập / tập", "Số 1…4 mỗi tập, sau đó sang tập mới", "Tập {Y} số {X}", "Số", "Tập", MaxX: 4, SortOrder: 3),
    ];

    protected override string EntityName => "Mẫu đánh số";

    protected override string? Describe(NumberingPattern entity) => entity.Name;

    protected override Expression<Func<NumberingPattern, PatternDto>> Projection => x => new PatternDto(
        x.Id, x.PublicId, x.Name, x.Description, x.LabelTemplate, x.NameX, x.NameY, x.NameZ, x.StepX, x.StepY, x.StepZ, x.MaxX, x.MaxY, x.MaxZ, x.ResetX, x.ResetY,
        x.ResetZ, x.SortOrder, x.CreatedAt, x.UpdatedAt);

    protected override NumberingPattern Create(PatternData request) => NumberingPattern.Create(request);

    protected override void Update(NumberingPattern entity, PatternData request) => entity.Update(request);

    protected override IQueryable<NumberingPattern> Filter(IQueryable<NumberingPattern> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<NumberingPattern> Order(IQueryable<NumberingPattern> query) => query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name);

    protected override async Task ValidateAsync(NumberingPattern entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Name == entity.Name && x.Id != entity.Id, ct))
            throw new ConflictException("NAME_EXISTS", $"Mẫu đánh số '{entity.Name}' đã có.");
    }

    protected override async Task EnsureDeletableAsync(NumberingPattern entity, CancellationToken ct)
    {
        if (await Db.Set<Subscription>().AnyAsync(s => s.PatternId == entity.Id, ct))
            throw new ConflictException("PATTERN_IN_USE", $"Mẫu '{entity.Name}' đang có đăng ký, không xoá được.");
    }

    public async Task<int> AddMissingDefaultsAsync(CancellationToken ct)
    {
        var existing = await Set.Select(x => x.Name).ToListAsync(ct);
        var added = 0;
        foreach (var data in Defaults.Where(d => !existing.Contains(d.Name)))
        {
            Set.Add(NumberingPattern.Create(data));
            added++;
        }
        await Db.SaveChangesAsync(ct);
        return added;
    }
}
