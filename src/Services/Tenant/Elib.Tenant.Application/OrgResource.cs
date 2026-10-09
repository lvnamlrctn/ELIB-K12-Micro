using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Tenant.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Tenant.Application;

public sealed class OrgSearch : CrudSearch
{
    public long? ParentId { get; set; }
}

public sealed record OrgRequest(string Name, long? ParentId, int? SortOrder, int? Status, string? Link);

public sealed record OrgDto(long Id, Guid PublicId, string Name, long? ParentId, int Level, int SortOrder, int Status, string? Link);

public sealed record OrgTreeNode(
    long Id, Guid PublicId, string Name, long? ParentId, string? ParentName, int Level, int SortOrder, int Status, string? Link,
    int ChildCount, IReadOnlyList<OrgTreeNode> Children)
{
    public bool HasChildren => ChildCount > 0;
}

public sealed record UpdateOrderRequest(Guid PublicId, int NewOrder);

public sealed record MoveOrgRequest(long? NewParentId, int NewOrder);

/// <summary>Cơ cấu tổ chức (monolith: OrgController). Ngoài 8 endpoint chuẩn: GetTree, UpdateOrder, Move, DeleteWithChildren.</summary>
public sealed class OrgResource(ICrudDbContext db) : CrudResource<OrgResource, Org, OrgSearch, OrgRequest, OrgDto>(db)
{
    protected override string EntityName => "Phòng ban";

    protected override string? Describe(Org entity) => entity.Name;

    protected override Expression<Func<Org, OrgDto>> Projection =>
        x => new OrgDto(x.Id, x.PublicId, x.Name, x.ParentId, x.Level, x.SortOrder, x.Status, x.Link);

    // Tạo/sửa cần tra nút cha trong DB — override AddAsync/UpdateAsync, hai hook đồng bộ dưới đây không được gọi.
    protected override Org Create(OrgRequest request) => throw new NotSupportedException();

    protected override void Update(Org entity, OrgRequest request) => throw new NotSupportedException();

    protected override IQueryable<Org> Filter(IQueryable<Org> query, OrgSearch search)
    {
        if (search.ParentId is { } parentId) query = query.Where(x => x.ParentId == parentId);
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311
        return query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<Org> Order(IQueryable<Org> query) => query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id);

    protected override async Task EnsureDeletableAsync(Org entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.ParentId == entity.Id, ct))
            throw new ConflictException("ORG_HAS_CHILDREN", "Tổ chức còn đơn vị con — xoá cả nhánh bằng DeleteWithChildren.");
    }

    public override async Task<OrgDto> AddAsync(OrgRequest request, CancellationToken ct)
    {
        var parent = await ParentAsync(request.ParentId, ct);
        var org = Org.Create(request.Name, parent, request.SortOrder ?? 0, request.Status, request.Link);
        Set.Add(org);
        await AuditAsync(org, CrudChange.Added, ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(org);
    }

    /// <summary>Sửa thông tin; đổi cha thì đi qua <see cref="MoveAsync"/> để cập nhật Level của cả nhánh.</summary>
    public override async Task<OrgDto> UpdateAsync(Guid publicId, OrgRequest request, CancellationToken ct)
    {
        var org = await LoadAsync(publicId, ct);
        org.Update(request.Name, request.SortOrder ?? org.SortOrder, request.Status, request.Link);
        if (request.ParentId != org.ParentId) await MoveCoreAsync(org, request.ParentId, org.SortOrder, ct);
        await AuditAsync(org, CrudChange.Updated, ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(org);
    }

    public async Task<IReadOnlyList<OrgTreeNode>> GetTreeAsync(OrgSearch search, CancellationToken ct)
    {
        // Cây luôn dựng từ toàn bộ nút (chỉ lọc trạng thái), để nút khớp từ khoá không mất đường về gốc.
        var all = await Query(new OrgSearch { Status = search.Status }).Take(SearchAllLimit).ToListAsync(ct);
        var names = all.ToDictionary(o => o.Id, o => o.Name);
        var byParent = all.ToLookup(o => o.ParentId is { } p && names.ContainsKey(p) ? p : (long?)null);

        var keep = search.Term is { } term ? MatchingWithAncestors(all, term) : null;

        IReadOnlyList<OrgTreeNode> Build(long? parentId) => byParent[parentId]
            .Where(o => keep is null || keep.Contains(o.Id))
            .OrderBy(o => o.SortOrder).ThenBy(o => o.Id)
            .Select(o =>
            {
                var children = Build(o.Id);
                return new OrgTreeNode(o.Id, o.PublicId, o.Name, o.ParentId, o.ParentId is { } p ? names.GetValueOrDefault(p) : null,
                    o.Level, o.SortOrder, o.Status, o.Link, children.Count, children);
            })
            .ToList();

        return Build(null);
    }

    public async Task UpdateOrderAsync(UpdateOrderRequest request, CancellationToken ct)
    {
        var org = await LoadAsync(request.PublicId, ct);
        org.Reorder(request.NewOrder);
        await AuditAsync(org, CrudChange.Updated, ct);
        await Db.SaveChangesAsync(ct);
    }

    public async Task<OrgDto> MoveAsync(Guid publicId, MoveOrgRequest request, CancellationToken ct)
    {
        var org = await LoadAsync(publicId, ct);
        await MoveCoreAsync(org, request.NewParentId, request.NewOrder, ct);
        await AuditAsync(org, CrudChange.Updated, ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(org);
    }

    public async Task DeleteWithChildrenAsync(Guid publicId, CancellationToken ct)
    {
        var org = await LoadAsync(publicId, ct);
        var descendants = await DescendantsAsync(org.Id, ct);
        Set.RemoveRange(descendants);
        Set.Remove(org);
        await AuditAsync(org, CrudChange.Deleted, ct); // một dòng nhật ký cho cả nhánh
        await Db.SaveChangesAsync(ct);
    }

    private async Task MoveCoreAsync(Org org, long? newParentId, int newOrder, CancellationToken ct)
    {
        var parent = await ParentAsync(newParentId, ct);
        var descendants = await DescendantsAsync(org.Id, ct);
        var delta = org.MoveTo(parent, newOrder, descendants.Select(d => d.Id).ToHashSet());
        if (delta != 0)
            foreach (var d in descendants) d.ShiftLevel(delta);
    }

    private async Task<Org?> ParentAsync(long? parentId, CancellationToken ct) =>
        parentId is { } id and > 0
            ? await Set.FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException("tổ chức cha", id)
            : null;

    /// <summary>Mọi con cháu (tracked). Cây nhỏ (vài trăm nút/đơn vị) — tải một lần rồi duyệt trong bộ nhớ.</summary>
    private async Task<List<Org>> DescendantsAsync(long rootId, CancellationToken ct)
    {
        var all = await Set.Where(o => o.ParentId != null).ToListAsync(ct);
        var children = all.ToLookup(o => o.ParentId!.Value);
        var result = new List<Org>();
        var stack = new Stack<long>([rootId]);
        while (stack.Count > 0)
            foreach (var child in children[stack.Pop()])
            {
                result.Add(child);
                stack.Push(child.Id);
            }
        return result;
    }

    private static HashSet<long> MatchingWithAncestors(List<Org> all, string term)
    {
        var byId = all.ToDictionary(o => o.Id);
        var keep = new HashSet<long>();
#pragma warning disable CA1862, CA1308 // so khớp trong bộ nhớ, term đã ToLowerInvariant
        foreach (var match in all.Where(o => o.Name.ToLowerInvariant().Contains(term)))
#pragma warning restore CA1862, CA1308
        {
            for (Org? node = match; node is not null && keep.Add(node.Id);)
                node = node.ParentId is { } p ? byId.GetValueOrDefault(p) : null;
        }
        return keep;
    }
}
