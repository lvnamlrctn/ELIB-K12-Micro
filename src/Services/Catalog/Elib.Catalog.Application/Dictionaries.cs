using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Catalog.Application;

public sealed record DictionaryRequest(string Value, string? Description = null);

public sealed record DictionaryDto(
    long Id, Guid PublicId, string Kind, string Value, string? Description, int UseCount, bool AutoAdded, bool IsReplica, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Gợi ý trong trình biên mục: giá trị + diễn giải (khung phân loại).</summary>
public sealed record DictionarySuggestion(string Value, string? Description);

/// <summary>
/// Từ điển biên mục của đơn vị (monolith: DicAuthor/DicPublisher/DicKeyword/DicClass, quyền DIC_*). Mỗi loại một màn và một mã quyền —
/// loại cố định theo lớp con, không nhận từ request. Giá trị không trùng (không phân biệt dấu/hoa thường); nhập được từ Excel.
/// </summary>
public abstract class DictionaryResource<TSelf>(ICrudDbContext db)
    : CrudResource<TSelf, DictionaryTerm, CrudSearch, DictionaryRequest, DictionaryDto>(db), ICrudImportable<DictionaryRequest>
    where TSelf : DictionaryResource<TSelf>
{
    protected abstract string Kind { get; }

    protected override string? Describe(DictionaryTerm entity) => entity.Value;

    protected override Expression<Func<DictionaryTerm, DictionaryDto>> Projection =>
        x => new DictionaryDto(x.Id, x.PublicId, x.Kind, x.Value, x.Description, x.UseCount, x.AutoAdded, x.IsReplica, x.CreatedAt, x.UpdatedAt);

    protected override DictionaryTerm Create(DictionaryRequest r) => DictionaryTerm.Create(Kind, r.Value, r.Description);

    protected override void Update(DictionaryTerm entity, DictionaryRequest r) => entity.Update(r.Value, r.Description);

    protected override IQueryable<DictionaryTerm> Filter(IQueryable<DictionaryTerm> query, CrudSearch search)
    {
        query = query.Where(x => x.Kind == Kind);
        return MarcRecord.Fold(search.Keyword) is { Length: > 0 } key
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
            ? query.Where(x => x.Key.Contains(key) || (x.Description != null && x.Description.ToLower().Contains(search.Term!)))
#pragma warning restore CA1862, CA1304, CA1311
            : query;
    }

    protected override IOrderedQueryable<DictionaryTerm> Order(IQueryable<DictionaryTerm> query) =>
        Kind == DictionaryKinds.Class ? query.OrderBy(x => x.Value) : query.OrderByDescending(x => x.UseCount).ThenBy(x => x.Value);

    protected override Task EnsureDeletableAsync(DictionaryTerm entity, CancellationToken ct) =>
        entity.IsReplica ? throw new BusinessRuleException(DictionaryTerm.ReadOnlyCode, DictionaryTerm.ReadOnlyMessage) : Task.CompletedTask;

    protected override async Task ValidateAsync(DictionaryTerm entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Kind == entity.Kind && x.Key == entity.Key && x.Id != entity.Id, ct))
            throw new ConflictException("DICTIONARY_VALUE_EXISTS", $"'{entity.Value}' đã có trong từ điển.");
    }

    public IReadOnlyList<CrudImportColumn> ImportColumns { get; } =
    [
        new("value", "Giá trị", true, "Tên tác giả / nhà xuất bản / từ khoá / ký hiệu phân loại", "Gia tri", "Ten", "Ky hieu"),
        new("description", "Diễn giải", false, "Khung phân loại: tên lớp; loại khác có thể để trống", "Dien giai", "Mo ta"),
    ];

    public DictionaryRequest MapImportRow(CrudImportRow row) =>
        new(row.Required("value", "Giá trị"), row.Get("description"));
}

public sealed class AuthorDictionaryResource(ICrudDbContext db) : DictionaryResource<AuthorDictionaryResource>(db)
{
    protected override string Kind => DictionaryKinds.Author;
    protected override string EntityName => "Từ điển tác giả";
}

public sealed class PublisherDictionaryResource(ICrudDbContext db) : DictionaryResource<PublisherDictionaryResource>(db)
{
    protected override string Kind => DictionaryKinds.Publisher;
    protected override string EntityName => "Từ điển nhà xuất bản";
}

public sealed class KeywordDictionaryResource(ICrudDbContext db) : DictionaryResource<KeywordDictionaryResource>(db)
{
    protected override string Kind => DictionaryKinds.Keyword;
    protected override string EntityName => "Từ điển từ khoá";
}

public sealed class ClassDictionaryResource(ICrudDbContext db) : DictionaryResource<ClassDictionaryResource>(db)
{
    protected override string Kind => DictionaryKinds.Class;
    protected override string EntityName => "Khung phân loại";
}

/// <summary>
/// Từ điển tự học + gợi ý. Khi lưu biểu ghi: giá trị mới xuất hiện (tác giả 100/110/700/710 $a, nhà xuất bản 260/264 $b, từ khoá
/// 650/653 $a, phân loại 082 $a) được thêm vào từ điển hoặc tăng số lần dùng. Ghi SAU khi biểu ghi đã lưu, lỗi trùng do hai cán bộ cùng
/// lưu một lúc thì bỏ qua — không bao giờ làm hỏng việc lưu biểu ghi.
/// </summary>
public sealed class DictionaryLearner(ICrudDbContext db)
{
    public const int MaxSuggestions = 20;

    /// <summary>Học các giá trị có trong <paramref name="after"/> mà chưa có trong <paramref name="before"/> (thêm mới: before rỗng).</summary>
    public async Task LearnAsync(IEnumerable<MarcField> before, IEnumerable<MarcField> after, CancellationToken ct)
    {
        var old = DictionaryExtractor.Extract(before).Select(x => (x.Kind, DictionaryTerm.KeyOf(x.Value))).ToHashSet();
        var added = DictionaryExtractor.Extract(after).Where(x => !old.Contains((x.Kind, DictionaryTerm.KeyOf(x.Value)))).ToList();
        await LearnAsync(added, ct);
    }

    public async Task LearnAsync(IReadOnlyList<(string Kind, string Value)> values, CancellationToken ct)
    {
        if (values.Count == 0) return;
        var set = db.Set<DictionaryTerm>();
        var counts = values.GroupBy(v => (v.Kind, Key: DictionaryTerm.KeyOf(v.Value))).ToDictionary(g => g.Key, g => (Value: g.First().Value, Count: g.Count()));
        var keys = counts.Keys.Select(k => k.Key).Distinct().ToList();
        try
        {
            foreach (var chunk in keys.Chunk(500))
            {
                var existing = await set.Where(t => chunk.Contains(t.Key)).ToListAsync(ct);
                foreach (var term in existing)
                    // Bản sao dùng chung (IsReplica) là "đã có": không thêm trùng, không ghi số lần dùng.
                    if (counts.Remove((term.Kind, term.Key), out var hit) && !term.IsReplica) term.Used(hit.Count);
            }
            foreach (var ((kind, _), (value, count)) in counts)
            {
                var term = DictionaryTerm.Create(kind, value, null, autoAdded: true);
                term.Used(count);
                set.Add(term);
            }
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Cán bộ khác vừa thêm cùng giá trị — bỏ lượt học này, biểu ghi đã lưu.
            foreach (var entry in ((DbContext)db).ChangeTracker.Entries<DictionaryTerm>().ToList()) entry.State = EntityState.Detached;
        }
    }

    /// <summary>Gợi ý theo tiền tố trước, rồi theo chứa; xếp theo số lần dùng (khung phân loại: theo ký hiệu).</summary>
    public async Task<IReadOnlyList<DictionarySuggestion>> SuggestAsync(string? kind, string? query, int? take, CancellationToken ct)
    {
        var k = (kind ?? "").Trim().ToLowerInvariant();
        _ = DictionaryKinds.Module(k);
        var key = MarcRecord.Fold(query);
        if (key.Length == 0) return [];
        var size = Math.Clamp(take ?? 10, 1, MaxSuggestions);
        var source = db.Set<DictionaryTerm>().AsNoTracking().Where(t => t.Kind == k);
        var ordered = k == DictionaryKinds.Class
            ? source.Where(t => t.Key.StartsWith(key) || t.Key.Contains(key)).OrderBy(t => t.Key.StartsWith(key) ? 0 : 1).ThenBy(t => t.Value)
            : source.Where(t => t.Key.StartsWith(key) || t.Key.Contains(" " + key) || t.Key.Contains(key))
                .OrderBy(t => t.Key.StartsWith(key) ? 0 : t.Key.Contains(" " + key) ? 1 : 2).ThenByDescending(t => t.UseCount).ThenBy(t => t.Value);
        return await ordered.Take(size).Select(t => new DictionarySuggestion(t.Value, t.Description)).ToListAsync(ct);
    }
}

public sealed record IsbdConfigRequest(long BibTypeId, IReadOnlyList<IsbdRule> Rules);

public sealed record IsbdConfigDto(long Id, Guid PublicId, long BibTypeId, IReadOnlyList<IsbdRule> Rules, bool IsReplica, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Cấu hình ISBD theo loại biểu ghi (monolith: ConfigIsbd + IsbdField + IsbdSubfield, quyền CONFIG_ISBD). Mỗi loại một cấu hình.</summary>
public sealed class IsbdConfigResource(ICrudDbContext db) : CrudResource<IsbdConfigResource, IsbdConfig, CrudSearch, IsbdConfigRequest, IsbdConfigDto>(db)
{
    protected override string EntityName => "Cấu hình ISBD";

    protected override string? Describe(IsbdConfig entity) => $"loại biểu ghi {entity.BibTypeId}: {entity.Rules.Count} quy tắc";

    protected override Expression<Func<IsbdConfig, IsbdConfigDto>> Projection =>
        x => new IsbdConfigDto(x.Id, x.PublicId, x.BibTypeId, x.Rules, x.IsReplica, x.CreatedAt, x.UpdatedAt);

    protected override IsbdConfig Create(IsbdConfigRequest r) => IsbdConfig.Create(r.BibTypeId, r.Rules);

    protected override void Update(IsbdConfig entity, IsbdConfigRequest r) => entity.Update(r.BibTypeId, r.Rules);

    /// <summary>
    /// Đơn vị tạo cấu hình riêng cho loại đang có bản sao dùng chung → bỏ bản sao trước (unique theo loại). Bản sao bị xoá và lưu riêng một bước TRƯỚC khi
    /// thêm cấu hình (soft-delete và insert cùng lô có thể bị sắp xếp ngược → vi phạm unique); cấu hình mới đã được kiểm hợp lệ trước khi xoá.
    /// </summary>
    protected override async Task<IsbdConfig> CreateAsync(IsbdConfigRequest request, CancellationToken ct)
    {
        var config = Create(request);
        await RemoveReplicaAsync(config.BibTypeId, ct);
        return config;
    }

    protected override async Task UpdateAsync(IsbdConfig entity, IsbdConfigRequest request, CancellationToken ct)
    {
        if (entity.IsReplica) throw new BusinessRuleException(DictionaryTerm.ReadOnlyCode, DictionaryTerm.ReadOnlyMessage);
        if (request.BibTypeId != entity.BibTypeId) await RemoveReplicaAsync(request.BibTypeId, ct);
        Update(entity, request);
    }

    protected override Task EnsureDeletableAsync(IsbdConfig entity, CancellationToken ct) =>
        entity.IsReplica ? throw new BusinessRuleException(DictionaryTerm.ReadOnlyCode, DictionaryTerm.ReadOnlyMessage) : Task.CompletedTask;

    private async Task RemoveReplicaAsync(long bibTypeId, CancellationToken ct)
    {
        var replicas = await Set.Where(x => x.BibTypeId == bibTypeId && x.IsReplica).ToListAsync(ct);
        if (replicas.Count == 0) return;
        Set.RemoveRange(replicas);
        await Db.SaveChangesAsync(ct);
    }

    protected override IOrderedQueryable<IsbdConfig> Order(IQueryable<IsbdConfig> query) => query.OrderBy(x => x.BibTypeId);

    protected override async Task ValidateAsync(IsbdConfig entity, CancellationToken ct)
    {
        if (!await Db.Set<BibType>().AnyAsync(t => t.Id == entity.BibTypeId, ct))
            throw new BusinessRuleException("BIB_TYPE_NOT_FOUND", "Loại biểu ghi đã chọn không còn.");
        if (await Set.AnyAsync(x => x.BibTypeId == entity.BibTypeId && x.Id != entity.Id, ct))
            throw new ConflictException("ISBD_CONFIG_EXISTS", "Loại biểu ghi này đã có cấu hình ISBD — sửa cấu hình đó.");
    }

    /// <summary>Quy tắc khởi đầu tương đương quy tắc mặc định — cán bộ sửa từ đó.</summary>
    public static IReadOnlyList<IsbdRule> Template() => Isbd.DefaultRules();

    /// <summary>Xem trước ISBD của một biểu ghi với quy tắc đang soạn (chưa lưu).</summary>
    public async Task<IReadOnlyList<string>> PreviewAsync(IsbdPreviewRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bib = await Db.Set<Bib>().AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.Mfn, ct) ?? throw new NotFoundException("biểu ghi", request.Mfn);
        var rules = (request.Rules ?? []).Select(IsbdRule.Validate).ToList();
        return Isbd.Build(bib.Fields, rules);
    }
}

public sealed record IsbdPreviewRequest(long Mfn, IReadOnlyList<IsbdRule>? Rules);
