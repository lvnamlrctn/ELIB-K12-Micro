using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Circulation;
using Elib.Contracts.Events.Holdings;
using Elib.Holdings.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Application;

/// <summary>
/// Tìm bản sách (monolith: màn "Tìm kiếm tài liệu" /admin/books và danh sách ĐKCB của biểu ghi). Keyword: đầu số ĐKCB hoặc
/// một phần nhan đề. <see cref="ItemStatus"/>: mã trạng thái (I/R/L/S/X) hoặc B = đang mượn. BarcodeFrom/To: khoảng ĐKCB (so theo chữ hoa).
/// </summary>
public sealed class ItemSearch : CrudSearch
{
    public long? Mfn { get; set; }
    public long? StoreId { get; set; }
    public string? ItemStatus { get; set; }
    public string? BarcodeFrom { get; set; }
    public string? BarcodeTo { get; set; }
}

/// <summary>Thêm một ĐKCB nhập tay (monolith: RegisterSingleBarcode) — cần Mfn, Barcode. Sửa: chỉ kho và ghi chú.</summary>
public sealed record ItemRequest(long? Mfn = null, string? Barcode = null, long? StoreId = null, string? Note = null);

public sealed record ItemDto(
    long Id, Guid PublicId, string Barcode, long Mfn, Guid BibPublicId, string? Title, string? Author, string? PublishYear, string? Ddc,
    long? StoreId, string? StoreCode, string? StoreName, string Status, string? Note, long Version, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt,
    bool OnLoan = false, string? LoanCardNo = null, DateTimeOffset? LoanDueAt = null);

/// <summary>
/// Đăng ký ĐKCB theo lô cho một biểu ghi (monolith: RegisterBarcodes / BarcodeNumbering): <see cref="Quantity"/> mã liên tiếp
/// "<see cref="Prefix"/> + số đệm 0 đủ <see cref="Digits"/> chữ số", bắt đầu từ <see cref="StartNumber"/> hoặc số lớn nhất đã có của tiền tố + 1.
/// </summary>
public sealed record RegisterItemsRequest(long Mfn, int Quantity, string? Prefix = null, int Digits = 6, long? StartNumber = null, long? StoreId = null);

/// <summary>Xếp giá theo danh sách id hoặc số ĐKCB quét được; <see cref="StoreId"/> có giá trị thì chuyển luôn kho.</summary>
public sealed record ShelveRequest(IReadOnlyList<Guid>? Ids = null, IReadOnlyList<string>? Barcodes = null, long? StoreId = null);

/// <summary><see cref="Skipped"/>: ĐKCB không ở trạng thái chưa xếp giá (đã xếp, mất, thanh lý…).</summary>
public sealed record ShelveResult(int Shelved, IReadOnlyList<string> NotFound, IReadOnlyList<string> Skipped);

public sealed record NextBarcode(string Prefix, long Number, string Barcode);

/// <summary>
/// Bản sách / số đăng ký cá biệt (monolith: Barcode, đăng ký ở CatalogueBookController, xếp giá ở StoreShelvingController).
/// Mọi thay đổi phát <see cref="ItemChanged"/> (outbox, cùng transaction) cho circulation/search.
/// </summary>
public sealed class ItemResource(ICrudDbContext db, BibSnapshots bibs, IPublishEndpoint publisher, ITenantContext tenant, ICurrentActor actor)
    : CrudResource<ItemResource, Item, ItemSearch, ItemRequest, ItemDto>(db)
{
    public const int MaxBatch = 1000;
    public const int MaxShelve = 2000;

    protected override string EntityName => "Bản sách";

    protected override string? Describe(Item entity) => $"ĐKCB {entity.Barcode} (MFN {entity.BibId})";

    protected override Expression<Func<Item, ItemDto>> Projection => x => new ItemDto(
        x.Id, x.PublicId, x.Barcode, x.BibId, x.BibPublicId,
        Db.Set<BibSnapshot>().Where(b => b.Mfn == x.BibId).Select(b => b.Title).FirstOrDefault(),
        Db.Set<BibSnapshot>().Where(b => b.Mfn == x.BibId).Select(b => b.Author).FirstOrDefault(),
        Db.Set<BibSnapshot>().Where(b => b.Mfn == x.BibId).Select(b => b.PublishYear).FirstOrDefault(),
        Db.Set<BibSnapshot>().Where(b => b.Mfn == x.BibId).Select(b => b.Ddc).FirstOrDefault(),
        x.StoreId,
        Db.Set<Store>().Where(s => s.Id == x.StoreId).Select(s => s.Code).FirstOrDefault(),
        Db.Set<Store>().Where(s => s.Id == x.StoreId).Select(s => s.Name).FirstOrDefault(),
        x.Status, x.Note, x.Version, x.CreatedAt, x.UpdatedAt, x.OnLoan, x.OnLoan ? x.LoanCardNo : null, x.OnLoan ? x.LoanDueAt : null);

    protected override Item Create(ItemRequest request) => throw new NotSupportedException("Bản sách tạo qua CreateAsync (cần tra biểu ghi).");

    protected override async Task<Item> CreateAsync(ItemRequest request, CancellationToken ct)
    {
        var mfn = request.Mfn ?? throw new BusinessRuleException("MFN_REQUIRED", "Chọn biểu ghi cần đăng ký cá biệt.");
        var item = Item.Register(await bibs.RequireAsync(mfn, ct), request.Barcode ?? "", request.StoreId);
        if (!string.IsNullOrWhiteSpace(request.Note)) item.Update(request.StoreId, request.Note);
        return item;
    }

    protected override void Update(Item entity, ItemRequest request) => entity.Update(request.StoreId, request.Note);

    protected override IQueryable<Item> Filter(IQueryable<Item> query, ItemSearch s)
    {
        if (s.Mfn is { } mfn) query = query.Where(x => x.BibId == mfn);
        if (s.StoreId is { } store) query = query.Where(x => x.StoreId == store);
        if (!string.IsNullOrWhiteSpace(s.ItemStatus))
        {
            var status = s.ItemStatus.Trim().ToUpperInvariant();
            query = status == Domain.ItemStatus.OnLoan ? query.Where(x => x.OnLoan) : query.Where(x => x.Status == status);
        }
#pragma warning disable CA1309 // EF chỉ dịch string.Compare(a, b) — so sánh theo collation của cột
        if (!string.IsNullOrWhiteSpace(s.BarcodeFrom))
        {
            var from = Item.Key(s.BarcodeFrom);
            query = query.Where(x => string.Compare(x.BarcodeKey, from) >= 0);
        }
        if (!string.IsNullOrWhiteSpace(s.BarcodeTo))
        {
            var to = Item.Key(s.BarcodeTo);
            query = query.Where(x => string.Compare(x.BarcodeKey, to) <= 0);
        }
#pragma warning restore CA1309
        if (s.Term is { } term)
        {
            var key = Item.Key(term);
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
            query = query.Where(x => x.BarcodeKey.StartsWith(key)
                || Db.Set<BibSnapshot>().Any(b => b.Mfn == x.BibId && b.Title.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return query;
    }

    protected override IOrderedQueryable<Item> Order(IQueryable<Item> query) => query.OrderBy(x => x.BarcodeKey).ThenBy(x => x.Id);

    protected override async Task ValidateAsync(Item entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.BarcodeKey == entity.BarcodeKey && x.Id != entity.Id, ct))
            throw new ConflictException("BARCODE_EXISTS", $"Số ĐKCB '{entity.Barcode}' đã có.");
        await EnsureStoreAsync(entity.StoreId, ct);
    }

    protected override async Task OnSavingAsync(Item entity, CrudChange change, CancellationToken ct)
    {
        if (change == CrudChange.Deleted) entity.MarkDeleted();
        await PublishAsync(entity, change == CrudChange.Deleted, await StoreNamesAsync(ct), ct);
    }

    /// <summary>Đăng ký ĐKCB theo lô. Cả lô bị từ chối nếu có mã trùng ĐKCB đã có; lưu một lần.</summary>
    public async Task<IReadOnlyList<ItemDto>> RegisterAsync(RegisterItemsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Quantity is < 1 or > MaxBatch)
            throw new BusinessRuleException("QUANTITY_INVALID", $"Số lượng đăng ký từ 1 đến {MaxBatch} bản mỗi lần.");
        var digits = Math.Clamp(request.Digits, 1, 18);
        var prefix = NormalizePrefix(request.Prefix);
        var bib = await bibs.RequireAsync(request.Mfn, ct);
        await EnsureStoreAsync(request.StoreId, ct);

        var start = request.StartNumber is > 0 ? request.StartNumber.Value : await MaxNumberAsync(prefix, ct) + 1;
        var values = Enumerable.Range(0, request.Quantity).Select(i => Item.Format(prefix, start + i, digits)).ToList();
        if (values[^1].Length > Item.MaxBarcodeLength)
            throw new BusinessRuleException("BARCODE_INVALID", $"Số ĐKCB dài quá {Item.MaxBarcodeLength} ký tự.");
        var keys = values.Select(Item.Key).ToList();
        var conflicts = await Set.Where(x => keys.Contains(x.BarcodeKey)).OrderBy(x => x.BarcodeKey).Select(x => x.Barcode).Take(5).ToListAsync(ct);
        if (conflicts.Count > 0)
            throw new ConflictException("BARCODE_EXISTS", $"Số ĐKCB đã có: {string.Join(", ", conflicts)} — chọn số bắt đầu khác.");

        var stores = await StoreNamesAsync(ct);
        var items = values.Select(v => Item.Register(bib, v, request.StoreId)).ToList();
        foreach (var item in items)
        {
            Set.Add(item);
            await PublishAsync(item, deleted: false, stores, ct);
        }
        if (AuditSink is not null)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(Item), EntityName, bib.BibPublicId, CrudChange.Added,
                $"Đăng ký {items.Count} ĐKCB {values[0]}–{values[^1]} cho MFN {bib.Mfn}"), ct);
        await Db.SaveChangesAsync(ct);

        var ids = items.Select(i => i.Id).ToList();
        return await Set.AsNoTracking().Where(x => ids.Contains(x.Id)).OrderBy(x => x.BarcodeKey).Select(Projection).ToListAsync(ct);
    }

    /// <summary>Số ĐKCB tiếp theo của tiền tố — hiện trước cho cán bộ khi đăng ký theo lô.</summary>
    public async Task<NextBarcode> NextAsync(string? prefix, int digits, CancellationToken ct)
    {
        var p = NormalizePrefix(prefix);
        var number = await MaxNumberAsync(p, ct) + 1;
        return new NextBarcode(p, number, Item.Format(p, number, Math.Clamp(digits, 1, 18)));
    }

    /// <summary>Xếp giá (monolith: StoreShelving/Shelve): chưa xếp giá → sẵn sàng phục vụ.</summary>
    public async Task<ShelveResult> ShelveAsync(ShelveRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ids = request.Ids ?? [];
        var barcodes = (request.Barcodes ?? []).Select(b => b?.Trim() ?? "").Where(b => b.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count + barcodes.Count is 0 or > MaxShelve)
            throw new BusinessRuleException("SHELVE_EMPTY", $"Chọn từ 1 đến {MaxShelve} bản sách cần xếp giá.");
        await EnsureStoreAsync(request.StoreId, ct);

        var keys = barcodes.Select(Item.Key).ToList();
        var items = await Set.Where(x => ids.Contains(x.PublicId) || keys.Contains(x.BarcodeKey)).ToListAsync(ct);
        var found = items.Select(i => i.BarcodeKey).ToHashSet(StringComparer.Ordinal);
        var notFound = barcodes.Where(b => !found.Contains(Item.Key(b))).ToList();
        var stores = await StoreNamesAsync(ct);
        var skipped = new List<string>();
        var shelved = 0;
        foreach (var item in items.OrderBy(i => i.BarcodeKey, StringComparer.Ordinal))
        {
            if (item.Status != ItemStatus.Unshelved)
            {
                skipped.Add(item.Barcode);
                continue;
            }
            item.Shelve(request.StoreId);
            await PublishAsync(item, deleted: false, stores, ct);
            shelved++;
        }
        if (shelved > 0 && AuditSink is not null)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(Item), EntityName, Guid.Empty, CrudChange.Updated, $"Xếp giá {shelved} bản sách"), ct);
        await Db.SaveChangesAsync(ct);
        return new ShelveResult(shelved, notFound, skipped);
    }

    /// <summary>
    /// Trạng thái hiện tại theo số ĐKCB, đúng dạng event <see cref="ItemChanged"/> — circulation gọi qua /internal khi bản sao
    /// chưa có bản sách (service mới triển khai, event chưa tới). Không có → null.
    /// </summary>
    public async Task<ItemChanged?> CurrentStateAsync(string barcode, CancellationToken ct)
    {
        var key = Item.Key(barcode);
        var item = await Set.AsNoTracking().FirstOrDefaultAsync(x => x.BarcodeKey == key, ct);
        return item is null ? null : ToEvent(item, deleted: false, await StoreNamesAsync(ct));
    }

    /// <summary>
    /// Lượt mượn từ circulation (LoanChanged): ghi "đang mượn" để hiển thị; lượt đóng vì mất tài liệu thì bản sách sang "Mất" và phát
    /// ItemChanged. Bản sách không có (đã xoá, khác đơn vị) → bỏ qua. Chưa lưu — nơi gọi SaveChanges.
    /// </summary>
    public async Task ApplyLoanAsync(LoanChanged e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var item = await Set.FirstOrDefaultAsync(x => x.PublicId == e.ItemPublicId, ct);
        if (item is null) return;
        if (!item.ApplyLoan(e.LoanPublicId, e.Version, e.CardNo, e.LoanedAt, e.DueAt, e.ReturnedAt is not null, e.ClosedItemStatus)) return;
        await PublishAsync(item, deleted: false, await StoreNamesAsync(ct), ct);
        if (AuditSink is not null)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(Item), EntityName, item.PublicId, CrudChange.Updated,
                $"ĐKCB {item.Barcode} chuyển trạng thái \"{ItemStatus.Names.GetValueOrDefault(item.Status)}\" (lưu thông báo mất, thẻ {e.CardNo})"), ct);
    }

    public const int MaxStatePage = 2000;

    /// <summary>Trạng thái mọi bản sách theo id tăng dần sau <paramref name="afterId"/> — search dựng chỉ mục lần đầu / dựng lại.</summary>
    public async Task<(IReadOnlyList<ItemChanged> Items, long? Next)> StatesAsync(long afterId, int take, CancellationToken ct)
    {
        var size = Math.Clamp(take, 1, MaxStatePage);
        var items = await Set.AsNoTracking().Where(x => x.Id > afterId).OrderBy(x => x.Id).Take(size).ToListAsync(ct);
        var stores = await StoreNamesAsync(ct);
        return (items.Select(i => ToEvent(i, deleted: false, stores)).ToList(), items.Count == size ? items[^1].Id : null);
    }

    private static string NormalizePrefix(string? prefix)
    {
        var p = (prefix ?? "").Trim().ToUpperInvariant();
        if (p.Length > 20 || p.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            throw new BusinessRuleException("PREFIX_INVALID", "Tiền tố ĐKCB tối đa 20 ký tự, không chứa khoảng trắng.");
        if (p.Length > 0 && char.IsAsciiDigit(p[^1]))
            throw new BusinessRuleException("PREFIX_INVALID", "Tiền tố ĐKCB không được kết thúc bằng chữ số (phần số do hệ thống đánh).");
        return p;
    }

    private async Task<long> MaxNumberAsync(string prefix, CancellationToken ct) =>
        await Set.Where(x => x.Prefix == prefix && x.Number != null).MaxAsync(x => x.Number, ct) ?? 0;

    private async Task EnsureStoreAsync(long? storeId, CancellationToken ct)
    {
        if (storeId is { } id && !await Db.Set<Store>().AnyAsync(s => s.Id == id, ct))
            throw new BusinessRuleException("STORE_NOT_FOUND", "Kho đã chọn không còn.");
    }

    private Task<Dictionary<long, string>> StoreNamesAsync(CancellationToken ct) =>
        Db.Set<Store>().AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);

    private Task PublishAsync(Item item, bool deleted, Dictionary<long, string> stores, CancellationToken ct)
    {
        if (item.PublicId == Guid.Empty) item.PublicId = Guid.CreateVersion7(); // bản mới: interceptor chỉ gán khi còn trống
        return publisher.Publish(ToEvent(item, deleted, stores), ct);
    }

    private ItemChanged ToEvent(Item item, bool deleted, Dictionary<long, string> stores) =>
        new()
        {
            TenantId = tenant.RequireTenantId(),
            Actor = new EventActor(actor.Id, actor.Kind),
            ItemPublicId = item.PublicId,
            Barcode = item.Barcode,
            BibPublicId = item.BibPublicId,
            Mfn = item.BibId,
            StoreId = item.StoreId,
            StoreName = item.StoreId is { } s && stores.TryGetValue(s, out var name) ? name : null,
            Status = item.Status,
            Deleted = deleted,
            Version = item.Version,
        };
}
