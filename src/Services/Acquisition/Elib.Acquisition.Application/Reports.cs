using System.Globalization;
using Elib.Acquisition.Domain;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Microsoft.EntityFrameworkCore;

namespace Elib.Acquisition.Application;

/// <summary>
/// Bộ lọc báo cáo bổ sung / tra cứu đơn nhận (monolith: AcquisitionReportRequest + ReceiptLookupRequest). Khoảng ngày lọc theo
/// <b>ngày nhận</b> của phiếu (riêng "thư mục sách mới": ngày biên mục). <see cref="StoreId"/>: kho của phiếu nhận ở danh sách / tra cứu;
/// kho của từng bản ở phân bổ kho và sổ ĐKCB.
/// </summary>
public sealed class AcquisitionReportRequest
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public long? StoreId { get; set; }
    public long? SupplierId { get; set; }
    public long? SourceId { get; set; }
    public long? FundId { get; set; }

    /// <summary>Mã phiếu nhận (= Id) từ … đến …</summary>
    public long? ReceiptCodeFrom { get; set; }

    public long? ReceiptCodeTo { get; set; }
    public string? ReceiptName { get; set; }

    /// <summary>true = đã hoàn tất, false = chưa (monolith: Status).</summary>
    public bool? Completed { get; set; }

    public long? CreatedBy { get; set; }
    public DateOnly? CreatedFrom { get; set; }
    public DateOnly? CreatedTo { get; set; }
    public long? MfnFrom { get; set; }
    public long? MfnTo { get; set; }
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Publisher { get; set; }
    public string? PublishYear { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Có điều kiện nào lọc theo phiếu nhận (không tính kho — kho của bản lọc ở holdings).</summary>
    internal bool HasReceiptFilter => From is not null || To is not null || SupplierId is not null || SourceId is not null || FundId is not null
        || ReceiptCodeFrom is not null || ReceiptCodeTo is not null || !string.IsNullOrWhiteSpace(ReceiptName) || Completed is not null
        || CreatedBy is not null || CreatedFrom is not null || CreatedTo is not null;
}

/// <summary>Một dòng của "Danh sách tài liệu bổ sung": dòng phiếu nhận kèm thành tiền (số bản × đơn giá × tỷ giá).</summary>
public sealed record AcquisitionListRow(
    long ReceiptId, string? ReceiptName, DateOnly ReceivedOn, long Mfn, string Title, string? Author, int Quantity, decimal? Price, string? Currency, decimal Rate,
    decimal Total);

/// <summary><see cref="Quantity"/> / <see cref="Amount"/> tính trên toàn bộ kết quả lọc, không chỉ trang hiện tại.</summary>
public sealed record AcquisitionListPage(IReadOnlyList<AcquisitionListRow> Items, int Total, int PageIndex, int PageSize, int Quantity, decimal Amount);

/// <summary>
/// Tra cứu đơn nhận theo dòng (monolith: LookupLines): mô tả ISBD rút gọn của tài liệu, đơn nhận, nguồn, kho.
/// <see cref="ReceiptPublicId"/>: mở chi tiết đơn từ cột "Mã đơn".
/// </summary>
public sealed record ReceiptLookupRow(
    long Mfn, long ReceiptId, string Isbd, DateTimeOffset CreatedAt, DateOnly ReceivedOn, string? SourceName, long? StoreId, string? StoreName, Guid BibPublicId,
    Guid ReceiptPublicId = default);

public sealed record StoreAllocationReport(IReadOnlyList<HoldingsStoreCount> Items, int Total);

public sealed record AccessionRow(
    string Barcode, long Mfn, string? Title, string? Author, string? Publisher, string? PublishYear, string? Ddc, string? StoreName, DateOnly Date, long? ReceiptId,
    decimal? Price, string? Currency, decimal? Rate);

public sealed record NewBookRow(long Mfn, string Title, string? Author, string? Publisher, string? PublishYear, DateOnly? CatalogedOn);

/// <summary>Số lượng theo biểu ghi — in nhãn gáy/môn loại trước khi đăng ký ĐKCB cụ thể (không có mã vạch).</summary>
public sealed record LabelQuantityRow(long Mfn, string Title, string? Author, string? Ddc, string? Cutter, int Quantity);

/// <summary>File Excel của các báo cáo bổ sung — tên thư viện lấy từ bản sao đơn vị trong DB của acquisition.</summary>
public sealed class AcquisitionExcel(ICrudDbContext db, ITenantContext tenant, TimeProvider clock) : ReportExcel(clock)
{
    public override async Task<string?> LibraryNameAsync(CancellationToken ct)
    {
        var id = tenant.RequireTenantId();
        return await db.Set<TenantReplicaRecord>().AsNoTracking().Where(t => t.TenantId == id).Select(t => t.Name).FirstOrDefaultAsync(ct);
    }
}

/// <summary>
/// Báo cáo bổ sung (monolith: AcquisitionReportController / AcquisitionReportService + CatalogueReceiptController.LookupLines). Acquisition
/// lọc phiếu nhận và dòng nhận (dữ liệu của chính nó); số bản theo kho và sổ ĐKCB do holdings (service giữ bản sách) trả theo các phiếu
/// khớp bộ lọc. Holdings không trả lời → 503 <c>HOLDINGS_UNAVAILABLE</c> ở phân bổ kho / sổ ĐKCB; danh sách và tra cứu vẫn chạy, chỉ thiếu tên kho.
/// </summary>
public sealed class AcquisitionReports(ICrudDbContext db, IHoldingsReceiptItems holdings, AcquisitionExcel excel, ITenantContext tenant)
{
    public const int MaxPageSize = 500;

    // Khởi tạo bằng thuộc tính (không dùng constructor) để EF còn dịch được các Where/OrderBy nối tiếp.
    private sealed class LineRow
    {
        public ReceiptLine Line { get; init; } = null!;
        public Receipt Receipt { get; init; } = null!;
        public BibSnapshot? Bib { get; init; }
    }

    private sealed record LookupRaw(
        long Mfn, long ReceiptId, string Title, string? Author, string? Publisher, string? PublishYear, DateTimeOffset CreatedAt, DateOnly ReceivedOn,
        string? SourceName, long? StoreId, Guid BibPublicId, Guid ReceiptPublicId);

    private static (int Page, int Size) Paging(AcquisitionReportRequest r) => (Math.Max(1, r.PageIndex), Math.Clamp(r.PageSize, 1, MaxPageSize));

    private static string? Term(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static void EnsureRange(DateOnly? from, DateOnly? to) => ReportExcel.EnsureRange(from, to);

    /// <summary>Phiếu nhận khớp bộ lọc. <paramref name="receiptStore"/>: lọc kho theo kho của phiếu (danh sách / tra cứu).</summary>
    private IQueryable<Receipt> Receipts(AcquisitionReportRequest r, bool receiptStore)
    {
        EnsureRange(r.From, r.To);
        EnsureRange(r.CreatedFrom, r.CreatedTo);
        var receipts = db.Set<Receipt>().AsNoTracking();
        if (r.From is { } from) receipts = receipts.Where(x => x.ReceivedOn >= from);
        if (r.To is { } to) receipts = receipts.Where(x => x.ReceivedOn <= to);
        if (r.SupplierId is { } supplier) receipts = receipts.Where(x => x.SupplierId == supplier);
        if (r.SourceId is { } source) receipts = receipts.Where(x => x.SourceId == source);
        if (r.FundId is { } fund) receipts = receipts.Where(x => x.FundId == fund);
        if (r.ReceiptCodeFrom is { } codeFrom) receipts = receipts.Where(x => x.Id >= codeFrom);
        if (r.ReceiptCodeTo is { } codeTo) receipts = receipts.Where(x => x.Id <= codeTo);
        if (receiptStore && r.StoreId is { } store) receipts = receipts.Where(x => x.StoreId == store);
        if (r.Completed is { } done) receipts = done ? receipts.Where(x => x.CompletedAt != null) : receipts.Where(x => x.CompletedAt == null);
        if (r.CreatedBy is { } by) receipts = receipts.Where(x => x.CreatedBy == by);
        if (r.CreatedFrom is { } createdFrom)
        {
            var start = VietnamDate.Start(createdFrom);
            receipts = receipts.Where(x => x.CreatedAt >= start);
        }
        if (r.CreatedTo is { } createdTo)
        {
            var end = VietnamDate.Start(createdTo.AddDays(1));
            receipts = receipts.Where(x => x.CreatedAt < end);
        }
        if (Term(r.ReceiptName) is { } name)
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
            receipts = receipts.Where(x => x.Name != null && x.Name.ToLower().Contains(name));
#pragma warning restore CA1862, CA1304, CA1311
        return receipts;
    }

    /// <summary>Dòng phiếu nhận thuộc các phiếu khớp bộ lọc và khớp bộ lọc biểu ghi.</summary>
    private IQueryable<LineRow> Lines(AcquisitionReportRequest r)
    {
        var lines =
            from l in db.Set<ReceiptLine>().AsNoTracking()
            join rc in Receipts(r, receiptStore: true) on l.ReceiptId equals rc.Id
            join b in db.Set<BibSnapshot>() on l.BibPublicId equals b.BibPublicId into bj
            from b in bj.DefaultIfEmpty()
            select new LineRow { Line = l, Receipt = rc, Bib = b };
        if (r.MfnFrom is { } mfnFrom) lines = lines.Where(x => x.Line.Mfn >= mfnFrom);
        if (r.MfnTo is { } mfnTo) lines = lines.Where(x => x.Line.Mfn <= mfnTo);
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        if (Term(r.Title) is { } title) lines = lines.Where(x => x.Line.Title.ToLower().Contains(title));
        if (Term(r.Author) is { } author) lines = lines.Where(x => x.Line.Author != null && x.Line.Author.ToLower().Contains(author));
        if (Term(r.Publisher) is { } publisher) lines = lines.Where(x => x.Bib != null && x.Bib.Publisher != null && x.Bib.Publisher.ToLower().Contains(publisher));
#pragma warning restore CA1862, CA1304, CA1311
        if (Term(r.PublishYear) is { } year) lines = lines.Where(x => x.Bib != null && x.Bib.PublishYear != null && x.Bib.PublishYear.Contains(year));
        return lines;
    }

    // ── 1. Danh sách tài liệu bổ sung ────────────────────────────────────────

    public async Task<AcquisitionListPage> ListAsync(AcquisitionReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lines = Lines(request);
        var (page, size) = Paging(request);
        var rows = await ListRows(lines).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new AcquisitionListPage(rows, await lines.CountAsync(ct), page, size, await lines.SumAsync(x => (int?)x.Line.Quantity, ct) ?? 0,
            await lines.SumAsync(x => (decimal?)(x.Line.Quantity * (x.Line.Price ?? 0m) * x.Line.Rate), ct) ?? 0m);
    }

    public async Task<ExcelFile> ListExportAsync(AcquisitionReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lines = Lines(request);
        var rows = await ListRows(lines).Take(ReportExcel.MaxRows).ToListAsync(ct);
        return await excel.BuildAsync("danh-sach-bo-sung", ct, new ExcelSheet("Danh sách bổ sung", "DANH SÁCH BỔ SUNG TÀI LIỆU", ReportExcel.Period(request.From, request.To),
            ["Mã phiếu", "Tên phiếu", "Ngày nhận", "MFN", "Nhan đề", "Tác giả", "Số lượng", "Đơn giá", "Tiền tệ", "Tỷ giá", "Thành tiền"],
            rows.Select(x => (IReadOnlyList<object?>)[x.ReceiptId, x.ReceiptName, x.ReceivedOn, x.Mfn, x.Title, x.Author, x.Quantity, x.Price, x.Currency, x.Rate, x.Total])));
    }

    private static IQueryable<AcquisitionListRow> ListRows(IQueryable<LineRow> lines) => lines
        .OrderByDescending(x => x.Receipt.ReceivedOn).ThenByDescending(x => x.Line.Id)
        .Select(x => new AcquisitionListRow(x.Receipt.Id, x.Receipt.Name, x.Receipt.ReceivedOn, x.Line.Mfn, x.Line.Title, x.Line.Author, x.Line.Quantity, x.Line.Price,
            x.Line.Currency, x.Line.Rate, x.Line.Quantity * (x.Line.Price ?? 0m) * x.Line.Rate));

    // ── 2. Tra cứu đơn nhận theo dòng ────────────────────────────────────────

    public async Task<CrudPage<ReceiptLookupRow>> LookupLinesAsync(AcquisitionReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lines = Lines(request);
        var (page, size) = Paging(request);
        var rows = await LookupRows(lines).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new CrudPage<ReceiptLookupRow>(await WithStoreNamesAsync([.. rows.Select(ToLookup)], ct), await lines.CountAsync(ct), page, size);
    }

    public async Task<ExcelFile> LookupLinesExportAsync(AcquisitionReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var raw = await LookupRows(Lines(request)).Take(ReportExcel.MaxRows).ToListAsync(ct);
        var rows = await WithStoreNamesAsync([.. raw.Select(ToLookup)], ct);
        return await excel.BuildAsync("tra-cuu-don-nhan", ct, new ExcelSheet("Tra cứu đơn nhận", "TRA CỨU ĐƠN NHẬN", ReportExcel.Period(request.From, request.To),
            ["MFN", "Đơn nhận", "Tài liệu", "Ngày nhận", "Ngày tạo", "Nguồn", "Kho"],
            rows.Select(x => (IReadOnlyList<object?>)[x.Mfn, x.ReceiptId, x.Isbd, x.ReceivedOn, x.CreatedAt, x.SourceName, x.StoreName])));
    }

    private IQueryable<LookupRaw> LookupRows(IQueryable<LineRow> lines) => lines
        .OrderByDescending(x => x.Receipt.CreatedAt).ThenByDescending(x => x.Line.Id)
        .Select(x => new LookupRaw(x.Line.Mfn, x.Receipt.Id, x.Bib == null ? x.Line.Title : x.Bib.Title, x.Bib == null ? x.Line.Author : x.Bib.Author,
            x.Bib == null ? null : x.Bib.Publisher, x.Bib == null ? null : x.Bib.PublishYear, x.Receipt.CreatedAt, x.Receipt.ReceivedOn,
            db.Set<AcquisitionSource>().Where(s => s.Id == x.Receipt.SourceId).Select(s => s.Name).FirstOrDefault(), x.Receipt.StoreId, x.Line.BibPublicId,
            x.Receipt.PublicId));

    private static ReceiptLookupRow ToLookup(LookupRaw x) =>
        new(x.Mfn, x.ReceiptId, Isbd(x.Title, x.Author, x.Publisher, x.PublishYear), x.CreatedAt, x.ReceivedOn, x.SourceName, x.StoreId, null, x.BibPublicId,
            x.ReceiptPublicId);

    /// <summary>Mô tả ISBD rút gọn: "Nhan đề / Tác giả .- NXB, Năm".</summary>
    public static string Isbd(string? title, string? author, string? publisher, string? year)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(title)) parts.Add(title);
        if (!string.IsNullOrWhiteSpace(author)) parts.Add("/ " + author);
        var pub = new[] { publisher, year }.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (pub.Count > 0) parts.Add(".- " + string.Join(", ", pub));
        return string.Join(' ', parts);
    }

    // ── 2b. In nhãn theo số lượng mỗi biểu ghi ───────────────────────────────

    public const int MaxLabelBibs = 2000;

    /// <summary>
    /// Số lượng theo biểu ghi của các dòng khớp bộ lọc (monolith: CatalogueReceiptController.LookupLinesForLabel) — in nhãn gáy/môn loại
    /// (DDC/Cutter) theo SỐ LƯỢNG đã đặt/nhận, dùng khi chưa đăng ký ĐKCB cụ thể nên không có mã vạch thật. Tối đa <see cref="MaxLabelBibs"/>
    /// biểu ghi mỗi lần — vượt thì thu hẹp bộ lọc.
    /// </summary>
    public async Task<IReadOnlyList<LabelQuantityRow>> LabelQuantitiesAsync(AcquisitionReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var grouped = await Lines(request).GroupBy(x => x.Line.BibPublicId)
            .Select(g => new
            {
                Mfn = g.Max(x => x.Line.Mfn),
                Title = g.Max(x => x.Bib == null ? x.Line.Title : x.Bib.Title),
                Author = g.Max(x => x.Bib == null ? x.Line.Author : x.Bib.Author),
                Ddc = g.Max(x => x.Bib == null ? null : x.Bib.Ddc),
                Cutter = g.Max(x => x.Bib == null ? null : x.Bib.Cutter),
                Quantity = g.Sum(x => x.Line.Quantity),
            })
            .OrderBy(x => x.Mfn).Take(MaxLabelBibs + 1).ToListAsync(ct);
        if (grouped.Count > MaxLabelBibs)
            throw new BusinessRuleException("REPORT_TOO_WIDE", $"Bộ lọc khớp quá {MaxLabelBibs:N0} biểu ghi — thu hẹp khoảng ngày hoặc bộ lọc.");
        return [.. grouped.Select(x => new LabelQuantityRow(x.Mfn, x.Title ?? "", x.Author, x.Ddc, x.Cutter, x.Quantity))];
    }

    // ── 3. Phân bổ kho ───────────────────────────────────────────────────────

    public async Task<StoreAllocationReport> StoreAllocationAsync(AcquisitionReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var counts = await holdings.StoreCountsAsync(tenant.RequireTenantId(), await HoldingsRequestAsync(request, 0, 0, ct), ct);
        return new StoreAllocationReport(counts, counts.Sum(c => c.Copies));
    }

    public async Task<ExcelFile> StoreAllocationExportAsync(AcquisitionReportRequest request, CancellationToken ct)
    {
        var report = await StoreAllocationAsync(request, ct);
        return await excel.BuildAsync("phan-bo-kho", ct, new ExcelSheet("Phân bổ kho", "BÁO CÁO PHÂN BỔ KHO", ReportExcel.Period(request.From, request.To),
            ["Kho", "Số bản"],
            [.. report.Items.Select(x => (IReadOnlyList<object?>)[x.StoreName ?? "(Chưa chọn kho)", x.Copies]), ["Tổng cộng", report.Total]]));
    }

    // ── 4. Sổ đăng ký cá biệt ────────────────────────────────────────────────

    public async Task<CrudPage<AccessionRow>> AccessionRegisterAsync(AcquisitionReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (page, size) = Paging(request);
        var result = await holdings.ReportAsync(tenant.RequireTenantId(), await HoldingsRequestAsync(request, (page - 1) * size, size, ct), ct);
        return new CrudPage<AccessionRow>(await AccessionRowsAsync(result.Items, ct), result.Total, page, size);
    }

    public async Task<ExcelFile> AccessionRegisterExportAsync(AcquisitionReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await holdings.ReportAsync(tenant.RequireTenantId(), await HoldingsRequestAsync(request, 0, Math.Min(ReportExcel.MaxRows, 20_000), ct), ct);
        var rows = await AccessionRowsAsync(result.Items, ct);
        return await excel.BuildAsync("so-dang-ky-ca-biet", ct, new ExcelSheet("Sổ ĐKCB", "SỔ ĐĂNG KÝ CÁ BIỆT", ReportExcel.Period(request.From, request.To),
            ["Số ĐKCB", "Nhan đề", "Tác giả", "Nhà xuất bản", "Năm XB", "Phân loại", "Kho", "Ngày nhận", "Đơn nhận", "Đơn giá", "Tiền tệ"],
            rows.Select(x => (IReadOnlyList<object?>)[x.Barcode, x.Title, x.Author, x.Publisher, x.PublishYear, x.Ddc, x.StoreName, x.Date, x.ReceiptId, x.Price, x.Currency])));
    }

    /// <summary>Bản sách → kèm ngày nhận / mã đơn nhận (phiếu) và đơn giá (dòng phiếu); bản không qua phiếu nhận lấy ngày đăng ký cá biệt.</summary>
    private async Task<IReadOnlyList<AccessionRow>> AccessionRowsAsync(IReadOnlyList<HoldingsReportRow> items, CancellationToken ct)
    {
        var receiptIds = items.Where(i => i.ReceiptPublicId is not null).Select(i => i.ReceiptPublicId!.Value).Distinct().ToList();
        var lineIds = items.Where(i => i.LinePublicId is not null).Select(i => i.LinePublicId!.Value).Distinct().ToList();
        var receipts = await db.Set<Receipt>().AsNoTracking().Where(x => receiptIds.Contains(x.PublicId))
            .Select(x => new { x.PublicId, x.Id, x.ReceivedOn }).ToDictionaryAsync(x => x.PublicId, ct);
        var lines = await db.Set<ReceiptLine>().AsNoTracking().Where(l => lineIds.Contains(l.PublicId))
            .Select(l => new { l.PublicId, l.Price, l.Currency, l.Rate }).ToDictionaryAsync(l => l.PublicId, ct);
        return [.. items.Select(i =>
        {
            var receipt = i.ReceiptPublicId is { } rid && receipts.TryGetValue(rid, out var rc) ? rc : null;
            var line = i.LinePublicId is { } lid && lines.TryGetValue(lid, out var l) ? l : null;
            return new AccessionRow(i.Barcode, i.Mfn, i.Title, i.Author, i.Publisher, i.PublishYear, i.Ddc, i.StoreName, receipt?.ReceivedOn ?? i.RegisteredOn,
                receipt?.Id, line?.Price, line?.Currency, line?.Rate);
        })];
    }

    /// <summary>Bộ lọc gửi holdings: có điều kiện theo phiếu nhận thì chỉ các phiếu khớp, không thì mọi bản.</summary>
    private async Task<HoldingsReportRequest> HoldingsRequestAsync(AcquisitionReportRequest r, int skip, int take, CancellationToken ct)
    {
        IReadOnlyList<Guid>? ids = null;
        if (r.HasReceiptFilter)
        {
            var found = await Receipts(r, receiptStore: false).Select(x => x.PublicId).Take(5001).ToListAsync(ct);
            if (found.Count > 5000)
                throw new BusinessRuleException("REPORT_TOO_WIDE", "Bộ lọc khớp quá 5.000 phiếu nhận — thu hẹp khoảng ngày hoặc bộ lọc.");
            ids = found;
        }
        return new HoldingsReportRequest(ids, r.StoreId, skip, take, r.MfnFrom, r.MfnTo, r.Title, r.Author, r.Publisher, r.PublishYear);
    }

    // ── 5. Thư mục sách mới ───────────────────────────────────────────────────

    /// <summary>Biểu ghi mới biên mục (ngày biên mục từ catalog); biểu ghi không rõ ngày xếp cuối như monolith.</summary>
    public async Task<CrudPage<NewBookRow>> NewBooksAsync(AcquisitionReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var books = NewBooks(request);
        var (page, size) = Paging(request);
        var rows = await NewBookRows(books).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new CrudPage<NewBookRow>([.. rows.Select(ToNewBook)], await books.CountAsync(ct), page, size);
    }

    public async Task<ExcelFile> NewBooksExportAsync(AcquisitionReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rows = (await NewBookRows(NewBooks(request)).Take(ReportExcel.MaxRows).ToListAsync(ct)).Select(ToNewBook);
        return await excel.BuildAsync("thu-muc-sach-moi", ct, new ExcelSheet("Thư mục sách mới", "DANH MỤC SÁCH MỚI", ReportExcel.Period(request.From, request.To),
            ["MFN", "Nhan đề", "Tác giả", "Nhà xuất bản", "Năm XB", "Ngày biên mục"],
            rows.Select(x => (IReadOnlyList<object?>)[x.Mfn, x.Title, x.Author, x.Publisher, x.PublishYear, x.CatalogedOn])));
    }

    private IQueryable<BibSnapshot> NewBooks(AcquisitionReportRequest r)
    {
        EnsureRange(r.From, r.To);
        var books = db.Set<BibSnapshot>().AsNoTracking().Where(b => !b.Deleted);
        if (r.From is { } from)
        {
            var start = VietnamDate.Start(from);
            books = books.Where(b => b.CatalogedAt != null && b.CatalogedAt >= start);
        }
        if (r.To is { } to)
        {
            var end = VietnamDate.Start(to.AddDays(1));
            books = books.Where(b => b.CatalogedAt != null && b.CatalogedAt < end);
        }
        return books;
    }

    private static IQueryable<BibSnapshot> NewBookRows(IQueryable<BibSnapshot> books) =>
        books.OrderBy(b => b.CatalogedAt == null).ThenByDescending(b => b.CatalogedAt).ThenByDescending(b => b.Mfn);

    private static NewBookRow ToNewBook(BibSnapshot b) =>
        new(b.Mfn, b.Title, b.Author, b.Publisher, b.PublishYear, b.CatalogedAt is { } at ? VietnamDate.Of(at) : null);

    // ── Tiện ích ─────────────────────────────────────────────────────────────

    /// <summary>Điền tên kho vào kết quả (phiếu nhận chỉ giữ id kho). Holdings không trả lời → để trống tên, vẫn trả dữ liệu.</summary>
    private async Task<IReadOnlyList<ReceiptLookupRow>> WithStoreNamesAsync(List<ReceiptLookupRow> rows, CancellationToken ct)
    {
        if (rows.All(r => r.StoreId is null)) return rows;
        IReadOnlyList<HoldingsStore> stores;
        try
        {
            stores = await holdings.StoresAsync(tenant.RequireTenantId(), ct);
        }
        catch (BusinessRuleException ex) when (ex.StatusCode >= 500)
        {
            return rows;
        }
        var names = stores.ToDictionary(s => s.Id, s => s.Name);
        return [.. rows.Select(r => r.StoreId is { } id && names.TryGetValue(id, out var name) ? r with { StoreName = name } : r)];
    }
}
