using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Catalog.Application;

public sealed record BibTypeRequest(string Name, string Code, string RecordType = "a", string BibLevel = "m");

public sealed record BibTypeDto(long Id, Guid PublicId, string Name, string Code, string RecordType, string BibLevel);

/// <summary>Loại biểu ghi (monolith: CatalogueBibType, quyền BIB_TYPES). Mã không trùng; còn biểu ghi/biểu mẫu dùng thì không xoá được.</summary>
public sealed class BibTypeResource(ICrudDbContext db) : CrudResource<BibTypeResource, BibType, CrudSearch, BibTypeRequest, BibTypeDto>(db)
{
    protected override string EntityName => "Loại biểu ghi";

    protected override string? Describe(BibType entity) => $"{entity.Code} — {entity.Name}";

    protected override Expression<Func<BibType, BibTypeDto>> Projection =>
        x => new BibTypeDto(x.Id, x.PublicId, x.Name, x.Code, x.RecordType, x.BibLevel);

    protected override BibType Create(BibTypeRequest r) => BibType.Create(r.Name, r.Code, r.RecordType, r.BibLevel);

    protected override void Update(BibType entity, BibTypeRequest r) => entity.Update(r.Name, r.Code, r.RecordType, r.BibLevel);

    protected override IQueryable<BibType> Filter(IQueryable<BibType> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term) || x.Code.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<BibType> Order(IQueryable<BibType> query) => query.OrderBy(x => x.Name);

    protected override async Task ValidateAsync(BibType entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Code == entity.Code && x.Id != entity.Id, ct))
            throw new ConflictException("BIB_TYPE_CODE_EXISTS", $"Mã loại biểu ghi '{entity.Code}' đã có.");
    }

    protected override async Task EnsureDeletableAsync(BibType entity, CancellationToken ct)
    {
        if (await Db.Set<Bib>().AnyAsync(b => b.BibTypeId == entity.Id, ct))
            throw new ConflictException("BIB_TYPE_IN_USE", $"Loại '{entity.Name}' đang có biểu ghi — chuyển biểu ghi sang loại khác trước khi xoá.");
        if (await Db.Set<Worksheet>().AnyAsync(w => w.BibTypeId == entity.Id, ct))
            throw new ConflictException("BIB_TYPE_IN_USE", $"Loại '{entity.Name}' đang được biểu mẫu biên mục dùng.");
    }

    /// <summary>Loại biểu ghi mặc định của thư viện trường học (mã, tên, Leader/06, Leader/07).</summary>
    public static readonly IReadOnlyList<BibTypeRequest> Defaults =
    [
        new("Sách", "SACH", "a", "m"),
        new("Sách giáo khoa", "SGK", "a", "m"),
        new("Báo, tạp chí", "TAPCHI", "a", "s"),
        new("Luận văn, luận án", "LUANVAN", "a", "m"),
        new("Bản đồ", "BANDO", "e", "m"),
        new("Tài liệu nghe nhìn", "NGHENHIN", "g", "m"),
        new("Tài liệu điện tử", "DIENTU", "m", "m"),
    ];

    /// <summary>Thêm loại mặc định còn thiếu (theo mã) — seed khi khởi tạo đơn vị, chạy lại không thêm trùng.</summary>
    public async Task<int> AddMissingDefaultsAsync(CancellationToken ct)
    {
        var existing = await Set.Select(t => t.Code).ToListAsync(ct);
        var missing = Defaults.Where(d => !existing.Contains(d.Code)).ToList();
        foreach (var d in missing) Set.Add(BibType.Create(d.Name, d.Code, d.RecordType, d.BibLevel));
        if (missing.Count > 0) await Db.SaveChangesAsync(ct);
        return missing.Count;
    }
}

public sealed class WorksheetSearch : CrudSearch
{
    public long? BibTypeId { get; set; }
}

public sealed record WorksheetRequest(string Name, long? BibTypeId, IReadOnlyList<MarcField> Fields);

public sealed record WorksheetDto(long Id, Guid PublicId, string Name, long? BibTypeId, IReadOnlyList<MarcField> Fields, DateTimeOffset? UpdatedAt);

/// <summary>Biểu mẫu biên mục (monolith: CatalogueWorkSheet + WorksheetField/Subfield, quyền WORKSHEETS).</summary>
public sealed class WorksheetResource(ICrudDbContext db) : CrudResource<WorksheetResource, Worksheet, WorksheetSearch, WorksheetRequest, WorksheetDto>(db)
{
    protected override string EntityName => "Biểu mẫu biên mục";

    protected override string? Describe(Worksheet entity) => entity.Name;

    protected override Expression<Func<Worksheet, WorksheetDto>> Projection =>
        x => new WorksheetDto(x.Id, x.PublicId, x.Name, x.BibTypeId, x.Fields, x.UpdatedAt ?? x.CreatedAt);

    protected override Worksheet Create(WorksheetRequest r) => Worksheet.Create(r.Name, r.BibTypeId, r.Fields);

    protected override void Update(Worksheet entity, WorksheetRequest r) => entity.Update(r.Name, r.BibTypeId, r.Fields);

    protected override IQueryable<Worksheet> Filter(IQueryable<Worksheet> query, WorksheetSearch search)
    {
        if (search.BibTypeId is { } type) query = query.Where(x => x.BibTypeId == type);
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<Worksheet> Order(IQueryable<Worksheet> query) => query.OrderBy(x => x.Name);

    protected override async Task ValidateAsync(Worksheet entity, CancellationToken ct)
    {
        if (entity.BibTypeId is { } type && !await Db.Set<BibType>().AnyAsync(t => t.Id == type, ct))
            throw new BusinessRuleException("BIB_TYPE_NOT_FOUND", "Loại biểu ghi đã chọn không còn.");
        if (await Set.AnyAsync(x => x.Name == entity.Name && x.Id != entity.Id, ct))
            throw new ConflictException("NAME_EXISTS", $"Biểu mẫu '{entity.Name}' đã có.");
    }

    /// <summary>Biểu mẫu theo loại biểu ghi (monolith: GetByBibType).</summary>
    public async Task<IReadOnlyList<WorksheetDto>> ByBibTypeAsync(long bibTypeId, CancellationToken ct) =>
        await Set.AsNoTracking().Where(x => x.BibTypeId == bibTypeId).OrderBy(x => x.Name).Select(Projection).ToListAsync(ct);

    /// <summary>Biểu mẫu mặc định theo mã loại biểu ghi — thêm cái còn thiếu (theo tên), loại chưa có thì bỏ qua.</summary>
    public async Task<int> AddMissingDefaultsAsync(CancellationToken ct)
    {
        var types = await Db.Set<BibType>().ToDictionaryAsync(t => t.Code, t => t.Id, ct);
        var existing = await Set.Select(w => w.Name).ToListAsync(ct);
        var added = 0;
        foreach (var (name, code, fields) in DefaultWorksheets.All)
        {
            if (existing.Contains(name) || !types.TryGetValue(code, out var typeId)) continue;
            Set.Add(Worksheet.Create(name, typeId, fields));
            added++;
        }
        if (added > 0) await Db.SaveChangesAsync(ct);
        return added;
    }
}

/// <summary>Biểu mẫu biên mục mặc định (thay dữ liệu Bib_Worksheet mẫu của monolith) — trường hay dùng ở thư viện trường học.</summary>
public static class DefaultWorksheets
{
    private static MarcField D(string tag, string ind1, string ind2, params string[] subfields) =>
        new(tag, ind1, ind2, Subfields: [.. subfields.Select(s => s.Split('=', 2) is var p ? new MarcSubfield(p[0], p.Length > 1 ? p[1] : "") : null!)]);

    private static readonly MarcField[] Book =
    [
        D("020", " ", " ", "a", "c"),
        D("041", "0", " ", "a=vie"),
        D("044", " ", " ", "a=vm"),
        D("082", "0", "4", "a", "b"),
        D("100", "0", " ", "a"),
        D("245", "1", "0", "a", "b", "c"),
        D("250", " ", " ", "a"),
        D("260", " ", " ", "a", "b", "c"),
        D("300", " ", " ", "a", "b", "c"),
        D("490", "0", " ", "a", "v"),
        D("500", " ", " ", "a"),
        D("520", " ", " ", "a"),
        D("650", " ", "7", "a"),
        D("653", " ", " ", "a"),
        D("700", "0", " ", "a", "e"),
    ];

    public static readonly IReadOnlyList<(string Name, string BibTypeCode, IReadOnlyList<MarcField> Fields)> All =
    [
        ("Sách", "SACH", Book),
        ("Sách giáo khoa", "SGK", [.. Book, D("521", " ", " ", "a"), D("526", "0", " ", "a", "b", "c")]),
        ("Báo, tạp chí", "TAPCHI",
        [
            D("022", " ", " ", "a"), D("041", "0", " ", "a=vie"), D("082", "0", "4", "a"), D("245", "0", "0", "a", "b", "c"),
            D("260", " ", " ", "a", "b", "c"), D("300", " ", " ", "a", "c"), D("310", " ", " ", "a"), D("362", "0", " ", "a"), D("650", " ", "7", "a"),
        ]),
        ("Luận văn, luận án", "LUANVAN",
        [
            D("041", "0", " ", "a=vie"), D("082", "0", "4", "a", "b"), D("100", "0", " ", "a"), D("245", "1", "0", "a", "b", "c"),
            D("260", " ", " ", "a", "b", "c"), D("300", " ", " ", "a", "c"), D("502", " ", " ", "a", "b", "c", "d"), D("520", " ", " ", "a"),
            D("650", " ", "7", "a"), D("653", " ", " ", "a"), D("700", "0", " ", "a", "e"),
        ]),
    ];
}
