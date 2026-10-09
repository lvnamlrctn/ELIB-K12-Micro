using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Patron.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Patron.Application;

public sealed class ReaderTypeResource(ICrudDbContext db) : NamedCatalogResource<ReaderTypeResource, ReaderType>(db)
{
    protected override string EntityName => "Loại bạn đọc";

    protected override async Task EnsureDeletableAsync(ReaderType entity, CancellationToken ct)
    {
        if (await Db.Set<Reader>().AnyAsync(r => r.ReaderTypeId == entity.Id, ct))
            throw new ConflictException("READER_TYPE_IN_USE", $"Loại bạn đọc '{entity.Name}' đang có bạn đọc — chuyển bạn đọc sang loại khác trước khi xoá.");
    }

    /// <summary>Loại bạn đọc mặc định của trường K12 — seed khi khởi tạo đơn vị, chạy lại không thêm trùng.</summary>
    public static readonly string[] Defaults = ["Học sinh", "Giáo viên", "Cán bộ, nhân viên"];

    public async Task<int> AddMissingDefaultsAsync(CancellationToken ct)
    {
        var existing = await Set.Select(t => t.Name).ToListAsync(ct);
        var missing = Defaults.Where(d => !existing.Contains(d)).ToList();
        foreach (var name in missing) Set.Add(NamedCatalogItem.New<ReaderType>(name));
        if (missing.Count > 0) await Db.SaveChangesAsync(ct);
        return missing.Count;
    }
}

public sealed class SchoolClassResource(ICrudDbContext db) : NamedCatalogResource<SchoolClassResource, SchoolClass>(db)
{
    protected override string EntityName => "Lớp";

    protected override async Task EnsureDeletableAsync(SchoolClass entity, CancellationToken ct)
    {
        if (await Db.Set<Reader>().AnyAsync(r => r.ClassId == entity.Id, ct))
            throw new ConflictException("CLASS_IN_USE", $"Lớp '{entity.Name}' đang có bạn đọc.");
    }
}

public sealed class CourseResource(ICrudDbContext db) : NamedCatalogResource<CourseResource, Course>(db)
{
    protected override string EntityName => "Khoá";

    protected override async Task EnsureDeletableAsync(Course entity, CancellationToken ct)
    {
        if (await Db.Set<Reader>().AnyAsync(r => r.CourseId == entity.Id, ct))
            throw new ConflictException("COURSE_IN_USE", $"Khoá '{entity.Name}' đang có bạn đọc.");
    }
}

public sealed record ReaderGroupRequest(string Name, string? Code);

public sealed record ReaderGroupDto(long Id, Guid PublicId, string Name, string? Code);

/// <summary>Nhóm bạn đọc (monolith: GroupReader) — tên + mã tuỳ chọn, mã không trùng.</summary>
public sealed class ReaderGroupResource(ICrudDbContext db)
    : CrudResource<ReaderGroupResource, ReaderGroup, CrudSearch, ReaderGroupRequest, ReaderGroupDto>(db)
{
    protected override string EntityName => "Nhóm bạn đọc";

    protected override string? Describe(ReaderGroup entity) => entity.Code is null ? entity.Name : $"{entity.Code} — {entity.Name}";

    protected override Expression<Func<ReaderGroup, ReaderGroupDto>> Projection => x => new ReaderGroupDto(x.Id, x.PublicId, x.Name, x.Code);

    protected override ReaderGroup Create(ReaderGroupRequest request) => ReaderGroup.Create(request.Name, request.Code);

    protected override void Update(ReaderGroup entity, ReaderGroupRequest request) => entity.Update(request.Name, request.Code);

    protected override IQueryable<ReaderGroup> Filter(IQueryable<ReaderGroup> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term) || (x.Code != null && x.Code.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<ReaderGroup> Order(IQueryable<ReaderGroup> query) => query.OrderBy(x => x.Name);

    protected override async Task ValidateAsync(ReaderGroup entity, CancellationToken ct)
    {
        if (entity.Code is { } code && await Set.AnyAsync(x => x.Code == code && x.Id != entity.Id, ct))
            throw new ConflictException("GROUP_CODE_EXISTS", $"Mã nhóm '{code}' đã có.");
    }
}
