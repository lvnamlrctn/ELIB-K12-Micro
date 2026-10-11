using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Search.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Search.Application;

/// <summary>Điều kiện tra Z39.50: Field = title | author | publisher | isbn | keyword | subject (BIB-1 Use 4/1003/1018/7/1016/21).</summary>
public sealed record Z3950Term(string Field, string Value);

/// <summary>Nơi tra: <see cref="SruUrl"/> có thì đi SRU (HTTP), không thì Z39.50 (TCP).</summary>
public sealed record Z3950Target(string Host, int Port, string Database, string RecordSyntax, string? UserName, string? Password, string? SruUrl);

/// <summary>Kết quả một máy chủ: tổng số bản ghi khớp và các bản ghi của trang yêu cầu.</summary>
public sealed record Z3950FetchResult(bool Connected, int Total, IReadOnlyList<Z3950Record> Records, string? Error);

/// <summary>Kết nối Z39.50/SRU ra máy chủ ngoài (Infrastructure). Lỗi mạng/máy chủ trả trong kết quả, không ném.</summary>
public interface IZ3950Client
{
    /// <summary>Tìm rồi lấy <paramref name="count"/> bản ghi từ vị trí <paramref name="start"/> (đếm từ 1).</summary>
    Task<Z3950FetchResult> SearchAsync(Z3950Target target, IReadOnlyList<Z3950Term> terms, int start, int count, CancellationToken ct);

    /// <summary>Thử kết nối (Z39.50 Init / SRU explain): null = được, không thì câu báo lỗi.</summary>
    Task<string?> TestAsync(Z3950Target target, CancellationToken ct);
}

public sealed class Z3950ServerSearch : CrudSearch
{
    public string? GroupName { get; set; }
}

/// <summary><see cref="Password"/>: khi sửa, null = giữ mật khẩu cũ, "" = bỏ mật khẩu.</summary>
public sealed record Z3950ServerRequest(
    string Name, string Host, int Port, string DatabaseName, string? RecordSyntax = null, string? UserName = null, string? Password = null,
    string? SruUrl = null, string? GroupName = null, bool ShowOnOpac = false);

/// <summary>Cấu hình máy chủ — không trả mật khẩu, chỉ báo đã có (<see cref="HasPassword"/>).</summary>
public sealed record Z3950ServerDto(
    long Id, Guid PublicId, string Name, string Host, int Port, string DatabaseName, string RecordSyntax, string? UserName, bool HasPassword,
    string? SruUrl, string? GroupName, bool ShowOnOpac, int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Máy chủ Z39.50/SRU của thư viện khác (monolith: OpacZ3950Config, quyền Z3950_CONFIGS).</summary>
public sealed class Z3950ServerResource(ICrudDbContext db) : CrudResource<Z3950ServerResource, Z3950Server, Z3950ServerSearch, Z3950ServerRequest, Z3950ServerDto>(db)
{
    protected override string EntityName => "Máy chủ Z39.50";

    protected override string? Describe(Z3950Server entity) => $"{entity.Name} ({entity.Host}:{entity.Port}/{entity.DatabaseName})";

    protected override Expression<Func<Z3950Server, Z3950ServerDto>> Projection => x => new Z3950ServerDto(
        x.Id, x.PublicId, x.Name, x.Host, x.Port, x.DatabaseName, x.RecordSyntax, x.UserName, x.Password != null, x.SruUrl, x.GroupName, x.ShowOnOpac,
        x.Status, x.CreatedAt, x.UpdatedAt);

    protected override Z3950Server Create(Z3950ServerRequest r) =>
        Z3950Server.Create(r.Name, r.Host, r.Port, r.DatabaseName, r.RecordSyntax, r.UserName, r.Password, r.SruUrl, r.GroupName, r.ShowOnOpac);

    protected override void Update(Z3950Server entity, Z3950ServerRequest r) =>
        entity.Update(r.Name, r.Host, r.Port, r.DatabaseName, r.RecordSyntax, r.UserName, r.Password, r.SruUrl, r.GroupName, r.ShowOnOpac);

    protected override IQueryable<Z3950Server> Filter(IQueryable<Z3950Server> query, Z3950ServerSearch search)
    {
        if (!string.IsNullOrWhiteSpace(search.GroupName))
        {
            var group = search.GroupName.Trim();
            query = query.Where(x => x.GroupName == group);
        }
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term) || x.Host.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<Z3950Server> Order(IQueryable<Z3950Server> query) => query.OrderBy(x => x.GroupName).ThenBy(x => x.Name).ThenBy(x => x.Id);

    protected override async Task ValidateAsync(Z3950Server entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Name == entity.Name && x.Id != entity.Id, ct))
            throw new ConflictException("Z3950_NAME_EXISTS", $"Đã có máy chủ tên '{entity.Name}'.");
    }
}

public sealed record Z3950SearchRequest
{
    public string? Title { get; init; }
    public string? Author { get; init; }
    public string? Publisher { get; init; }
    public string? Isbn { get; init; }
    public string? Keyword { get; init; }
    public string? Subject { get; init; }

    /// <summary>Máy chủ cần tra (PublicId); trống = mọi máy chủ đang hoạt động (OPAC: đang hiện trên OPAC). Tối đa 10.</summary>
    public IReadOnlyList<Guid>? ServerIds { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

/// <summary>Bản ghi tìm được: thông tin tóm tắt và MARC đầy đủ (xem trước, nạp vào màn biên mục).</summary>
public sealed record Z3950Hit(int Position, string? Title, string? Author, string? Publisher, string? Year, string? Isbn, Z3950Record Record);

public sealed record Z3950ServerResult(
    Guid ServerId, string ServerName, string RecordSyntax, bool Connected, int Total, int Page, int PageSize, IReadOnlyList<Z3950Hit> Hits, string? Error);

public sealed record Z3950ServerOption(Guid PublicId, string Name, string? GroupName, string RecordSyntax);

public sealed record Z3950TestResult(bool Ok, string? Error, long ElapsedMs);

/// <summary>
/// Tra cứu liên thư viện (monolith: OpacZ3950Controller cho biên mục, PublicSearchZ3950Controller cho OPAC): tra song song các máy chủ đã chọn,
/// mỗi máy chủ một danh sách kết quả và phân trang riêng (mỗi trang là một phiên Z39.50 mới — không giữ phiên ở server).
/// </summary>
public sealed class Z3950Search(ISearchDb db, IZ3950Client client, TimeProvider clock)
{
    public const int MaxServers = 10;
    public const int MaxPageSize = 20;

    /// <summary>Kết quả cuối danh sách được lấy — máy chủ lớn (Library of Congress) có hàng triệu bản ghi, lật tới đây là đủ.</summary>
    public const int MaxPosition = 1000;

    private IQueryable<Z3950Server> Active => db.Set<Z3950Server>().AsNoTracking().Where(s => s.Status == IHasStatus.Active);

    public async Task<IReadOnlyList<Z3950ServerOption>> ServersAsync(bool opacOnly, CancellationToken ct) =>
        await Active.Where(s => !opacOnly || s.ShowOnOpac).OrderBy(s => s.GroupName).ThenBy(s => s.Name)
            .Select(s => new Z3950ServerOption(s.PublicId, s.Name, s.GroupName, s.RecordSyntax)).ToListAsync(ct);

    public async Task<IReadOnlyList<Z3950ServerResult>> SearchAsync(Z3950SearchRequest request, bool opacOnly, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var terms = Terms(request);
        if (terms.Count == 0) throw new BusinessRuleException("Z3950_TERMS_REQUIRED", "Nhập ít nhất một điều kiện tìm.");
        var size = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var page = Math.Max(1, request.Page);
        var start = (page - 1) * size + 1;
        if (start > MaxPosition) throw new BusinessRuleException("Z3950_PAGE_TOO_FAR", $"Chỉ xem được {MaxPosition} kết quả đầu — thu hẹp điều kiện tìm.");

        var query = Active.Where(s => !opacOnly || s.ShowOnOpac);
        if (request.ServerIds is { Count: > 0 } ids)
        {
            var wanted = ids.Distinct().Take(MaxServers).ToList();
            query = query.Where(s => wanted.Contains(s.PublicId));
        }
        var servers = await query.OrderBy(s => s.GroupName).ThenBy(s => s.Name).Take(MaxServers).ToListAsync(ct);
        if (servers.Count == 0)
            throw new BusinessRuleException("Z3950_NO_SERVER", opacOnly ? "Thư viện chưa mở tra cứu liên thư viện." : "Chưa có máy chủ Z39.50 nào đang hoạt động.");

        var count = Math.Min(size, MaxPosition - start + 1);
        var results = await Task.WhenAll(servers.Select(async s =>
        {
            var fetched = await client.SearchAsync(Target(s), terms, start, count, ct);
            var unimarc = s.RecordSyntax == "UNIMARC";
            var hits = fetched.Records.Select((r, i) =>
            {
                var (title, author, publisher, year, isbn) = r.Summary(unimarc);
                return new Z3950Hit(start + i, title, author, publisher, year, isbn, r);
            }).ToList();
            return new Z3950ServerResult(s.PublicId, s.Name, s.RecordSyntax, fetched.Connected, Math.Min(fetched.Total, MaxPosition), page, size, hits, fetched.Error);
        }));
        return results;
    }

    /// <summary>Thử kết nối một máy chủ (nút "Kiểm tra" ở màn cấu hình).</summary>
    public async Task<Z3950TestResult> TestAsync(Guid publicId, CancellationToken ct)
    {
        var server = await db.Set<Z3950Server>().AsNoTracking().FirstOrDefaultAsync(s => s.PublicId == publicId, ct)
            ?? throw new NotFoundException("Máy chủ Z39.50", publicId);
        var started = clock.GetTimestamp();
        var error = await client.TestAsync(Target(server), ct);
        return new Z3950TestResult(error is null, error, (long)clock.GetElapsedTime(started).TotalMilliseconds);
    }

    private static Z3950Target Target(Z3950Server s) => new(s.Host, s.Port, s.DatabaseName, s.RecordSyntax, s.UserName, s.Password, s.SruUrl);

    private static List<Z3950Term> Terms(Z3950SearchRequest r)
    {
        var terms = new List<Z3950Term>();
        void Add(string field, string? value)
        {
            var v = (value ?? "").Trim();
            if (v.Length > 0) terms.Add(new Z3950Term(field, v.Length <= 200 ? v : v[..200]));
        }
        Add("title", r.Title);
        Add("author", r.Author);
        Add("publisher", r.Publisher);
        Add("isbn", new string([.. (r.Isbn ?? "").Where(c => char.IsAsciiDigit(c) || c is 'x' or 'X')]));
        Add("keyword", r.Keyword);
        Add("subject", r.Subject);
        return terms;
    }
}
