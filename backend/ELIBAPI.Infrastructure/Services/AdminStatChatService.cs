using System.Runtime.CompilerServices;
using System.Text.Json;
using ELIBAPI.Core.DTOs.Chat;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ELIBAPI.Core.Common;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>
/// Trợ lý thống kê cho cán bộ quản trị. Số liệu TỔNG (từ trước đến nay) theo phân hệ được phép xem được dựng 1
/// lần và cache vài phút (trước đây chạy lại ~14 truy vấn COUNT ở mọi câu hỏi); câu hỏi theo khoảng thời gian
/// ("hôm nay", "tháng 8", "quý trước") đi qua công cụ <c>get_period_stats</c>; yêu cầu LIỆT KÊ (đang mượn, quá hạn,
/// bạn đọc quá hạn, tài liệu mượn/đọc nhiều) đi qua <c>get_list</c> — bảng được dựng thẳng từ CSDL và trả nguyên,
/// không qua Gemini lượt 2 (tên/số liệu không bị model chép sai, đỡ một lượt gọi).
///
/// Nguồn số liệu (khớp báo cáo lưu thông <c>CirculationReportBuilder</c>):
/// <list type="bullet">
/// <item>Phiếu đang mượn (<see cref="OpenLoans"/>): <c>BookOuts</c> có Status khác "R". Trả qua hệ thống hiện tại đặt
/// Status = "R" và GIỮ dòng BookOut (CirculationLoanService.ReturnAsync); dữ liệu cũ thì xoá dòng khi trả; Status cũ
/// "1"/"O" đều là đang mượn. Quá hạn: DueDate &lt; hôm nay.</item>
/// <item>Lượt mượn (đã phát sinh) = phiếu đang mượn + <c>BookIns</c> (mỗi lượt trả, dù kiểu cũ hay mới, có đúng 1 BookIn)
/// theo BorrowDate — không đếm BookOut "R" để khỏi trùng. Lượt trả: <c>BookIns</c> (ReturnDate).</item>
/// <item>Lượt vào/ra thư viện: <c>CheckOuts</c> (đã ra) + <c>CheckIns</c> (đang ở trong) — KHÔNG phải lượt mượn.</item>
/// <item>Bản sách sẵn sàng cho mượn: <c>Barcodes.Status == "R"</c>, đang được mượn: "B".</item>
/// </list>
/// <para>Port ELIB-LRC 09-29/09-30. Tenant (K12): mọi truy vấn đi qua accessor có phạm vi đơn vị của người hỏi
/// (<c>tenantId</c> null = tài khoản hệ thống, xem toàn bộ); ĐKCB join theo (mã, đơn vị); cache số liệu tách theo đơn vị.
/// Model không tự chọn đơn vị — đơn vị lấy từ JWT ở controller.</para>
/// Dữ liệu nghiệp vụ được ghi theo giờ thư viện (<see cref="LibraryClock"/>, Asia/Ho_Chi_Minh) nên mốc ngày
/// ("hôm nay", "tháng này") dùng thẳng <see cref="LibraryClock.Today"/> — đúng cả khi máy chủ chạy UTC lẫn UTC+7.
/// </summary>
public class AdminStatChatService(
    ELIBAPIDbContext db,
    IPermissionService permService,
    IGeminiClient gemini,
    IMemoryCache cache,
    ILogger<AdminStatChatService> logger) : IAdminStatChatService
{
    private const string PeriodStatsTool = "get_period_stats";
    private const string ListTool        = "get_list";
    public const int ListDefaultSize = 20;
    public const int ListMaxSize     = 50;
    private const string NoAnswer = "Không thể tổng hợp số liệu thống kê lúc này.";
    private static readonly TimeSpan SnapshotTtl   = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PermissionTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan LlmTimeout    = TimeSpan.FromSeconds(45);

    // ── Phạm vi đơn vị của request (service scoped theo request; gán ở đầu AskStats*) ──
    private long? _tenantId;
    private string TenantKey => _tenantId?.ToString() ?? "all";
    private IQueryable<Core.Entities.Ebook.EbookItem> EbookItems => db.EbookItems.Where(x => _tenantId == null || x.TenantId == _tenantId);
    private IQueryable<Core.Entities.Ebook.EbookItemLoan> EbookItemLoans => db.EbookItemLoans.Where(x => _tenantId == null || x.TenantId == _tenantId);
    private IQueryable<Core.Entities.PrintBook.Bib> Bibs => db.Bibs.Where(x => _tenantId == null || x.TenantId == _tenantId);
    private IQueryable<Core.Entities.PrintBook.Barcode> Barcodes => db.Barcodes.Where(x => _tenantId == null || x.TenantId == _tenantId);
    private IQueryable<Core.Entities.PrintBook.BookOut> BookOuts => db.BookOuts.Where(x => _tenantId == null || x.TenantId == _tenantId);
    private IQueryable<Core.Entities.PrintBook.BookIn> BookIns => db.BookIns.Where(x => _tenantId == null || x.TenantId == _tenantId);
    private IQueryable<Core.Entities.PrintBook.CFineTicket> CFineTickets => db.CFineTickets.Where(x => _tenantId == null || x.TenantId == _tenantId);
    private IQueryable<Core.Entities.PrintBook.CheckOut> CheckOuts => db.CheckOuts.Where(x => _tenantId == null || x.TenantId == _tenantId);
    private IQueryable<Core.Entities.PrintBook.CheckIn> CheckIns => db.CheckIns.Where(x => _tenantId == null || x.TenantId == _tenantId);
    private IQueryable<Core.Entities.Dbo.Reader> Readers => db.Readers.Where(x => _tenantId == null || x.TenantId == _tenantId);
    private IQueryable<Core.Entities.PrintBook.AbReceipt> AbReceipts => db.AbReceipts.Where(x => _tenantId == null || x.TenantId == _tenantId);
    private IQueryable<Core.Entities.PrintBook.AbOrder> AbOrders => db.AbOrders.Where(x => _tenantId == null || x.TenantId == _tenantId);

    /// <summary>Phiếu mượn chưa trả (xem quy ước ở đầu lớp).</summary>
    private IQueryable<Core.Entities.PrintBook.BookOut> OpenLoans => BookOuts.Where(x => x.IsDelete != 2 && x.Status != "R");

    private sealed record Access(bool Digital, bool Circulation, bool Readers, bool Cataloging)
    {
        public List<string> Modules()
        {
            var m = new List<string>();
            if (Digital)     m.Add("Tài liệu số & Ebook");
            if (Circulation) m.Add("Mượn trả & Lưu thông");
            if (Readers)     m.Add("Quản lý bạn đọc");
            if (Cataloging)  m.Add("Biên mục & Sách in");
            return m;
        }
        public string Key => $"{(Digital ? 1 : 0)}{(Circulation ? 1 : 0)}{(Readers ? 1 : 0)}{(Cataloging ? 1 : 0)}";
    }

    /// <summary>Kết quả chuẩn bị: hoặc có ngay câu trả lời, hoặc payload để Gemini viết câu trả lời cuối.</summary>
    private sealed record Prepared(string? ImmediateAnswer, object? AnswerPayload, List<string> Modules, string Fallback);

    public async Task<AdminStatChatResponse> AskStatsAsync(long userId, long? tenantId, string question, List<ChatTurn>? history = null, CancellationToken ct = default)
    {
        _tenantId = tenantId;
        var p = await PrepareAsync(userId, question, history, ct);
        var result = new AdminStatChatResponse { PermittedModules = p.Modules };
        if (p.ImmediateAnswer != null) { result.Answer = p.ImmediateAnswer; return result; }
        try
        {
            using var cts = LinkedTimeout(ct);
            var part = await gemini.GenerateAsync(p.AnswerPayload!, cts.Token);
            result.Answer = part.TryGetProperty("text", out var t) && t.GetString() is { Length: > 0 } s ? s : NoAnswer;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Lỗi gọi Gemini cho AdminStatChat (lượt trả lời)");
            result.Answer = p.Fallback;
        }
        return result;
    }

    public async IAsyncEnumerable<ChatStreamEvent> AskStatsStreamAsync(long userId, long? tenantId, string question, List<ChatTurn>? history = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _tenantId = tenantId;
        var p = await PrepareAsync(userId, question, history, ct);
        if (p.ImmediateAnswer != null)
        {
            yield return ChatStreamEvent.Delta(p.ImmediateAnswer);
            yield return ChatStreamEvent.Done();
            yield break;
        }

        var any = false;
        string? error = null;
        await using (var e = gemini.StreamTextAsync(p.AnswerPayload!, ct).GetAsyncEnumerator(ct))
        {
            while (true)
            {
                bool has;
                try { has = await e.MoveNextAsync(); }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    logger.LogError(ex, "Lỗi stream Gemini cho AdminStatChat");
                    error = any ? "Kết nối tới máy chủ AI bị gián đoạn — câu trả lời có thể chưa đầy đủ." : null;
                    break;
                }
                if (!has) break;
                any = true;
                yield return ChatStreamEvent.Delta(e.Current);
            }
        }
        if (error != null) { yield return ChatStreamEvent.Error(error); yield break; }
        // Gemini lỗi ngay từ đầu → vẫn trả số liệu thô như bản không stream.
        if (!any) yield return ChatStreamEvent.Delta(p.Fallback);
        yield return ChatStreamEvent.Done();
    }

    // ── Chuẩn bị: quyền → số liệu tổng (cache) → lượt 1 (có công cụ theo kỳ) ─────────────

    private async Task<Prepared> PrepareAsync(long userId, string question, List<ChatTurn>? history, CancellationToken ct)
    {
        var access = await GetAccessAsync(userId);
        var modules = access.Modules();
        if (modules.Count == 0)
            return new Prepared("Bạn chưa được phân quyền truy cập bất kỳ phân hệ quản trị nào (Tài liệu số, Mượn trả, Bạn đọc, Biên mục) để tra cứu thống kê số liệu.",
                null, modules, "");

        var snapshot = await cache.GetOrCreateAsync("statchat:snapshot:" + TenantKey + ":" + access.Key, e =>
        {
            e.AbsoluteExpirationRelativeToNow = SnapshotTtl;
            return BuildSnapshotAsync(access);
        }) ?? "";
        var fallback = "### Dữ liệu thống kê hệ thống (trực tiếp từ CSDL):\n\n" +
                       string.Join("\n\n", snapshot.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => "- " + s));

        var today = LibraryClock.Today;
        var systemPrompt = BuildSystemPrompt(today, snapshot);
        var normalizedHistory = ChatHistory.Normalize(history);

        JsonElement turn1;
        try
        {
            using var cts = LinkedTimeout(ct);
            turn1 = await gemini.GenerateAsync(new
            {
                system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = ChatHistory.ToGeminiContents(normalizedHistory, question),
                generationConfig = GeminiGeneration.Config(0.2, 800),
                tools = BuildToolsPayload()
            }, cts.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Lỗi gọi Gemini cho AdminStatChat");
            return new Prepared(fallback, null, modules, fallback);
        }

        if (turn1.TryGetProperty("functionCall", out var functionCall)
            && functionCall.TryGetProperty("name", out var fnName) && fnName.GetString() == PeriodStatsTool)
        {
            var args = functionCall.TryGetProperty("args", out var a) ? a : default;
            var (from, toExclusive, label) = ParsePeriodArgs(args, today);
            var periodSummary = await BuildPeriodStatsSummaryAsync(from, toExclusive, label, access);

            // Lượt trả lời: đưa số liệu theo kỳ vào tin nhắn (không kèm tools → luôn ra văn bản, stream được).
            var message = $"{question}\n\n[SỐ LIỆU THEO KHOẢNG THỜI GIAN — lấy trực tiếp từ CSDL]\n{periodSummary}";
            var payload = new
            {
                system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = ChatHistory.ToGeminiContents(normalizedHistory, message),
                generationConfig = GeminiGeneration.Config(0.2, 800)
            };
            return new Prepared(null, payload, modules, fallback + "\n\n" + periodSummary);
        }

        if (turn1.TryGetProperty("functionCall", out var listCall)
            && listCall.TryGetProperty("name", out var listName) && listName.GetString() == ListTool)
        {
            var args = listCall.TryGetProperty("args", out var a) ? a : default;
            var table = await SafeLineAsync("Danh sách", () => BuildListAsync(args, today, access));
            return new Prepared(table, null, modules, table);
        }

        var answer = turn1.TryGetProperty("text", out var textProp) ? textProp.GetString() : null;
        return new Prepared(string.IsNullOrWhiteSpace(answer) ? NoAnswer : answer, null, modules, fallback);
    }

    private async Task<Access> GetAccessAsync(long userId) =>
        await cache.GetOrCreateAsync("statchat:access:" + userId, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = PermissionTtl;
            return new Access(
                await HasAny(userId, "DIGITAL_DOC", "EBOOK", "EBOOK_LOAN_MANAGE"),
                await HasAny(userId, "BORROW", "CIRC_REPORT", "CIRCULATION", "LOAN_HISTORY"),
                await HasAny(userId, "READERS", "READER_PARAMS"),
                await HasAny(userId, "CATALOG_BIBS", "CATALOGING", "AB_RECEIPTS"));
        }) ?? new Access(false, false, false, false);

    private async Task<bool> HasAny(long userId, params string[] moduleCodes)
    {
        foreach (var code in moduleCodes)
            if (await permService.HasPermissionAsync(userId, code, "view"))
                return true;
        return false;
    }

    private static string BuildSystemPrompt(DateTime today, string snapshot) =>
        "Bạn là Trợ lý Thống kê Dữ liệu Quản trị Thư viện. Hôm nay là " + VietnameseWeekday(today) + ", ngày " +
        today.ToString("dd/MM/yyyy") + ".\n" +
        "SỐ LIỆU TỔNG (từ trước đến nay, lấy trực tiếp từ CSDL) của các phân hệ người dùng được phép xem:\n" +
        snapshot + "\n\n" +
        "QUY TẮC:\n" +
        "1. Chỉ dùng số liệu được cung cấp (tổng ở trên, hoặc số liệu theo khoảng thời gian do công cụ trả về). " +
        "Số liệu nào không có thì nói rõ là hệ thống chưa có số liệu đó — TUYỆT ĐỐI không ước đoán hay tự tính ra con số mới " +
        "(trừ phép cộng/trừ/tỷ lệ đơn giản từ chính các số đã cho).\n" +
        "2. Câu hỏi về phân hệ KHÔNG có trong danh sách số liệu trên (và không ghi 'tạm thời không lấy được') là phân hệ " +
        "người dùng chưa được cấp quyền — từ chối lịch sự.\n" +
        $"3. Câu hỏi về MỘT KHOẢNG THỜI GIAN (hôm nay, hôm qua, tuần này, tuần trước, tháng này, tháng 8, quý 3, năm 2025, " +
        $"từ ngày… đến ngày…) → gọi {PeriodStatsTool} với fromDate/toDate (yyyy-MM-dd) tự tính từ ngày hôm nay ở trên.\n" +
        $"4. Yêu cầu LIỆT KÊ / DANH SÁCH / 'những ai', 'cuốn nào', 'top' (tài liệu đang mượn, đang mượn quá hạn, bạn đọc quá hạn, " +
        $"tài liệu mượn nhiều, tài liệu số đọc nhiều) → gọi {ListTool}. Số liệu tổng ở trên KHÔNG chứa danh sách — đừng tự liệt kê.\n" +
        "5. Trả lời tiếng Việt, ngắn gọn, đi thẳng vào số liệu; Markdown gọn (gạch đầu dòng, in đậm số liệu chính).\n" +
        "6. 'Lượt vào/ra thư viện' khác 'lượt mượn sách' — không lẫn hai loại số liệu này.";

    // ── Số liệu tổng ─────────────────────────────────────────────────────────

    private async Task<string> BuildSnapshotAsync(Access access)
    {
        var lines = new List<string>();
        var now = LibraryClock.Now;

        if (access.Digital)
            lines.Add(await SafeLineAsync("Tài liệu số", async () =>
            {
                var items = await EbookItems.Where(x => x.IsDelete != 2).GroupBy(_ => 1)
                    .Select(g => new { Total = g.Count(), Published = g.Count(x => x.Status == 2) }).FirstOrDefaultAsync();
                var loans = await EbookItemLoans.Where(x => x.IsDelete != 2).GroupBy(_ => 1)
                    .Select(g => new { Total = g.Count(), Active = g.Count(x => x.Status == 1 && (x.ExpiresAt == null || x.ExpiresAt > now)) })
                    .FirstOrDefaultAsync();
                return $"[Phân hệ Tài liệu số]: {items?.Total ?? 0} tài liệu số (đã xuất bản, hiện trên OPAC: {items?.Published ?? 0}; đang ẩn: {(items?.Total ?? 0) - (items?.Published ?? 0)}). " +
                       $"Tổng lượt mượn đọc tài liệu số: {loans?.Total ?? 0} (đang trong thời hạn mượn: {loans?.Active ?? 0}).";
            }));

        if (access.Circulation)
            lines.Add(await SafeLineAsync("Mượn trả & Lưu thông", async () =>
            {
                var todayStart = LibraryClock.Today;
                var bibs = await Bibs.CountAsync(x => x.IsDelete != 2);
                var copies = await Barcodes.Where(x => x.IsDelete != 2).GroupBy(_ => 1)
                    .Select(g => new { Total = g.Count(), Ready = g.Count(x => x.Status == "R"), Borrowed = g.Count(x => x.Status == "B") })
                    .FirstOrDefaultAsync();
                var loans = await OpenLoans.GroupBy(_ => 1)
                    .Select(g => new
                    {
                        Open = g.Count(),
                        Overdue = g.Count(x => x.DueDate != null && x.DueDate < todayStart),
                        OverdueReaders = g.Where(x => x.DueDate != null && x.DueDate < todayStart).Select(x => x.ReaderId).Distinct().Count()
                    }).FirstOrDefaultAsync();
                var returned = await BookIns.CountAsync(x => x.IsDelete != 2);
                var fines = await CFineTickets.Where(x => x.IsDelete != 2).GroupBy(_ => 1)
                    .Select(g => new { Count = g.Count(), Amount = g.Sum(x => x.TotalAmount ?? 0) }).FirstOrDefaultAsync();
                var visits = await CheckOuts.CountAsync(x => x.IsDelete != 2);
                var inside = await CheckIns.CountAsync(x => x.IsDelete != 2 && x.CheckInTime >= todayStart);
                return $"[Phân hệ Mượn trả & Lưu thông sách in]: {bibs} biểu ghi sách in, {copies?.Total ?? 0} bản sách " +
                       $"(sẵn sàng cho mượn: {copies?.Ready ?? 0}, đang được mượn: {copies?.Borrowed ?? 0}). " +
                       $"Tổng {(loans?.Open ?? 0) + returned} lượt mượn sách in đã phát sinh (đã trả: {returned}; đang mượn chưa trả: {loans?.Open ?? 0}, " +
                       $"trong đó quá hạn: {loans?.Overdue ?? 0} bản của {loans?.OverdueReaders ?? 0} bạn đọc). " +
                       $"Tổng {fines?.Count ?? 0} phiếu phạt, tổng tiền phạt {(fines?.Amount ?? 0).ToString("N0", Vi)} đ. " +
                       $"Tổng {visits} lượt vào/ra thư viện đã ghi nhận; hiện có {inside} bạn đọc đang ở trong thư viện (vào hôm nay, chưa ra).";
            }));

        if (access.Readers)
            lines.Add(await SafeLineAsync("Bạn đọc", async () =>
            {
                var today = LibraryClock.Today;
                var r = await Readers.Where(x => x.IsDelete != 2).GroupBy(_ => 1)
                    .Select(g => new
                    {
                        Total = g.Count(),
                        Active = g.Count(x => x.Status == 2),
                        Expired = g.Count(x => x.ExpireDate != null && x.ExpireDate < today)
                    }).FirstOrDefaultAsync();
                return $"[Phân hệ Bạn đọc]: {r?.Total ?? 0} bạn đọc (đang hoạt động: {r?.Active ?? 0}, thẻ đã hết hạn: {r?.Expired ?? 0}).";
            }));

        if (access.Cataloging)
            lines.Add(await SafeLineAsync("Bổ sung & Biên mục", async () =>
            {
                var receipts = await AbReceipts.CountAsync(x => x.IsDelete != 2);
                var orders = await AbOrders.CountAsync(x => x.IsDelete != 2);
                return $"[Phân hệ Bổ sung & Biên mục]: {orders} đơn đặt mua sách và {receipts} chứng từ nhập kho.";
            }));

        return string.Join("\n", lines);
    }

    /// <summary>Phân hệ lỗi truy vấn vẫn được nêu tên (không bỏ trống) — nếu bỏ trống, model sẽ tưởng người dùng
    /// không có quyền và từ chối sai.</summary>
    private async Task<string> SafeLineAsync(string module, Func<Task<string>> build)
    {
        try { return await build(); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi lấy thống kê {Module}", module);
            return $"[Phân hệ {module}]: tạm thời không lấy được số liệu (lỗi truy vấn).";
        }
    }

    // ── Số liệu theo khoảng thời gian ────────────────────────────────────────

    /// <summary>Đọc fromDate/toDate (yyyy-MM-dd, NGÀY THƯ VIỆN) Gemini tự tính. toDate là ngày cuối (bao gồm) nên
    /// cộng 1 ngày làm mốc "&lt;". Chặn khoảng vượt quá hôm nay hoặc dài quá 5 năm.</summary>
    public static (DateTime from, DateTime toExclusive, string label) ParsePeriodArgs(JsonElement args, DateTime today)
    {
        var fromStr = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("fromDate", out var f) ? f.GetString() : null;
        var toStr   = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("toDate", out var to) ? to.GetString() : null;
        var label   = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("label", out var l) ? l.GetString() : null;

        var from  = DateTime.TryParse(fromStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var fd) ? fd.Date : today.AddMonths(-1).Date;
        var toEnd = DateTime.TryParse(toStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var td) ? td.Date : today.Date;

        if (toEnd > today.Date) toEnd = today.Date;
        if (from > toEnd) (from, toEnd) = (toEnd, from);
        if ((toEnd - from).TotalDays > 5 * 365) from = toEnd.AddYears(-5);

        return (from, toEnd.AddDays(1), label ?? $"{from:dd/MM/yyyy} - {toEnd:dd/MM/yyyy}");
    }

    private async Task<string> BuildPeriodStatsSummaryAsync(DateTime from, DateTime toExclusive, string label, Access access)
    {
        // Khoảng theo ngày thư viện — cùng hệ giờ với dữ liệu (LibraryClock) nên so sánh trực tiếp.
        var f = from;
        var t = toExclusive;
        var lines = new List<string> { $"Số liệu trong khoảng {label} ({from:dd/MM/yyyy} đến {toExclusive.AddDays(-1):dd/MM/yyyy}):" };

        if (access.Digital)
            lines.Add(await SafeLineAsync("Tài liệu số", async () =>
            {
                var loans = await EbookItemLoans.CountAsync(x => x.IsDelete != 2 && x.CheckedOutAt >= f && x.CheckedOutAt < t);
                return $"[Tài liệu số]: {loans} lượt mượn đọc tài liệu số.";
            }));

        if (access.Circulation)
            lines.Add(await SafeLineAsync("Mượn trả & Lưu thông", async () =>
            {
                var borrows = await OpenLoans.CountAsync(x => x.BorrowDate >= f && x.BorrowDate < t)
                            + await BookIns.CountAsync(x => x.IsDelete != 2 && x.BorrowDate >= f && x.BorrowDate < t);
                var returns = await BookIns.CountAsync(x => x.IsDelete != 2 && x.ReturnDate >= f && x.ReturnDate < t);
                var lateReturns = await BookIns.CountAsync(x => x.IsDelete != 2 && x.ReturnDate >= f && x.ReturnDate < t
                                                                  && x.DueDate != null && x.ReturnDate > x.DueDate);
                var fines = await CFineTickets.Where(x => x.IsDelete != 2 && x.FineDate >= f && x.FineDate < t).GroupBy(_ => 1)
                    .Select(g => new { Count = g.Count(), Amount = g.Sum(x => x.TotalAmount ?? 0) }).FirstOrDefaultAsync();
                var visits = await CheckOuts.CountAsync(x => x.IsDelete != 2 && x.CheckInTime >= f && x.CheckInTime < t)
                           + await CheckIns.CountAsync(x => x.IsDelete != 2 && x.CheckInTime >= f && x.CheckInTime < t);
                return $"[Mượn trả sách in]: {borrows} lượt mượn, {returns} lượt trả (trong đó {lateReturns} lượt trả quá hạn). " +
                       $"{fines?.Count ?? 0} phiếu phạt, tổng {(fines?.Amount ?? 0).ToString("N0", Vi)} đ. {visits} lượt vào thư viện.";
            }));

        if (access.Readers)
            lines.Add(await SafeLineAsync("Bạn đọc", async () =>
            {
                var newReaders = await Readers.CountAsync(x => x.IsDelete != 2 && x.CreatedRowDate >= f && x.CreatedRowDate < t);
                return $"[Bạn đọc]: {newReaders} bạn đọc mới đăng ký.";
            }));

        if (access.Cataloging)
            lines.Add(await SafeLineAsync("Bổ sung & Biên mục", async () =>
            {
                var receipts = await AbReceipts.CountAsync(x => x.IsDelete != 2 && x.CreatedRowDate >= f && x.CreatedRowDate < t);
                var orders   = await AbOrders.CountAsync(x => x.IsDelete != 2 && x.CreatedRowDate >= f && x.CreatedRowDate < t);
                return $"[Biên mục & Bổ sung]: {orders} đơn đặt mua và {receipts} chứng từ nhập kho.";
            }));

        return string.Join("\n", lines);
    }

    // ── Danh sách (công cụ get_list) ─────────────────────────────────────────

    /// <summary>Dựng bảng Markdown trực tiếp từ CSDL cho yêu cầu liệt kê. Trả nguyên cho người dùng (không qua
    /// Gemini) — danh sách dài, tên người/nhan đề phải đúng từng chữ.</summary>
    private async Task<string> BuildListAsync(JsonElement args, DateTime today, Access access)
    {
        string? Str(string name) => args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var v)
                                    && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        var type = Str("listType") ?? "";
        var limit = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("limit", out var l)
                    && l.ValueKind == JsonValueKind.Number && l.TryGetDouble(out var n) ? (int)n : ListDefaultSize;
        limit = Math.Clamp(limit, 1, ListMaxSize);
        // Chỉ lọc theo thời gian khi model truyền ít nhất 1 mốc.
        (DateTime From, DateTime ToExclusive, string Label)? period =
            Str("fromDate") != null || Str("toDate") != null ? ParsePeriodArgs(args, today) : null;

        var digitalList = type == "top_ebooks";
        if (digitalList ? !access.Digital : !access.Circulation)
            return $"Bạn chưa được cấp quyền xem phân hệ {(digitalList ? "Tài liệu số" : "Mượn trả & Lưu thông")} nên không thể xem danh sách này.";

        return type switch
        {
            "current_loans"   => await ListLoansAsync(overdueOnly: false, limit, today, period),
            "overdue_loans"   => await ListLoansAsync(overdueOnly: true, limit, today, period),
            "overdue_readers" => await ListOverdueReadersAsync(limit, today),
            "top_borrowed"    => await ListTopBorrowedAsync(limit, period),
            "top_ebooks"      => await ListTopEbooksAsync(limit, period),
            _ => "Chưa hỗ trợ loại danh sách này. Có thể xem: tài liệu đang mượn, tài liệu quá hạn, bạn đọc quá hạn, " +
                 "sách in mượn nhiều, tài liệu số đọc nhiều."
        };
    }

    private const string CirculationReportLink = "\n\n_Xem đầy đủ, lọc và xuất Excel tại [Báo cáo lưu thông](/admin/circulation-report)._";

    private async Task<string> ListLoansAsync(bool overdueOnly, int limit, DateTime today, (DateTime From, DateTime ToExclusive, string Label)? period)
    {
        var todayStart = today;
        var q = OpenLoans;
        if (overdueOnly) q = q.Where(x => x.DueDate != null && x.DueDate < todayStart);
        if (period is { } p)
        {
            var (f, t) = (p.From, p.ToExclusive);
            q = q.Where(x => x.BorrowDate >= f && x.BorrowDate < t);
        }

        var total = await q.CountAsync();
        var what = overdueOnly ? "tài liệu đang mượn quá hạn" : "tài liệu đang mượn";
        var scope = period is { } pp ? $" (mượn trong khoảng {pp.Label})" : "";
        if (total == 0) return $"Hiện không có {what}{scope}.";

        var joined =
            from bo in q
            join rd in Readers on bo.ReaderId equals rd.Id into rdj from rd in rdj.DefaultIfEmpty()
            join bc in Barcodes.Where(b => b.IsDelete != 2) on new { V = bo.Barcode, T = bo.TenantId } equals new { V = bc.BarcodeValue, T = bc.TenantId } into bcj from bc in bcj.DefaultIfEmpty()
            join bx in db.BibXmls on bc.BibId equals bx.BibId into bxj from bx in bxj.DefaultIfEmpty()
            select new
            {
                bo.Id, bo.Barcode, bo.BorrowDate, bo.DueDate,
                Title = bx != null ? bx.Title : null,
                ReaderName = rd != null ? rd.FirstName + " " + rd.LastName : null,
                CardNo = rd != null ? rd.Cardno : null
            };
        // Quá hạn: lâu nhất trước; đang mượn: mới mượn trước. Lấy dư rồi bỏ trùng (ĐKCB trùng mã ở nhiều dòng Barcode).
        var ordered = overdueOnly ? joined.OrderBy(x => x.DueDate).ThenBy(x => x.Id) : joined.OrderByDescending(x => x.BorrowDate).ThenByDescending(x => x.Id);
        var list = (await ordered.Take(limit * 2).ToListAsync()).DistinctBy(x => x.Id).Take(limit).ToList();

        var head = $"**Danh sách {what}**{scope} — tổng **{Num(total)}** bản" +
                   (total > list.Count ? $", hiển thị {list.Count} bản {(overdueOnly ? "quá hạn lâu nhất" : "mượn gần nhất")}" : "") + ":\n\n";
        var table = overdueOnly
            ? MarkdownTable(["#", "Nhan đề", "ĐKCB", "Bạn đọc", "Hạn trả", "Quá hạn"],
                list.Select((x, i) => new[]
                {
                    (i + 1).ToString(), x.Title, x.Barcode, ReaderLabel(x.ReaderName, x.CardNo), FormatDate(x.DueDate),
                    x.DueDate is { } d ? $"{(today - d.Date).Days} ngày" : ""
                }))
            : MarkdownTable(["#", "Nhan đề", "ĐKCB", "Bạn đọc", "Ngày mượn", "Hạn trả"],
                list.Select((x, i) => new[]
                {
                    (i + 1).ToString(), x.Title, x.Barcode, ReaderLabel(x.ReaderName, x.CardNo), FormatDate(x.BorrowDate), FormatDate(x.DueDate)
                }));
        return head + table + CirculationReportLink;
    }

    private async Task<string> ListOverdueReadersAsync(int limit, DateTime today)
    {
        var todayStart = today;
        var grouped = OpenLoans
            .Where(x => x.ReaderId != null && x.DueDate != null && x.DueDate < todayStart)
            .GroupBy(x => x.ReaderId!.Value)
            .Select(g => new { ReaderId = g.Key, Count = g.Count(), MinDue = g.Min(x => x.DueDate) });

        var total = await grouped.CountAsync();
        if (total == 0) return "Hiện không có bạn đọc nào đang giữ sách quá hạn.";

        var top = await grouped.OrderBy(x => x.MinDue).ThenByDescending(x => x.Count).ThenBy(x => x.ReaderId).Take(limit).ToListAsync();
        var ids = top.Select(x => x.ReaderId).ToList();
        var readers = await Readers.Where(r => ids.Contains(r.Id))
            .Select(r => new { r.Id, r.FirstName, r.LastName, r.Cardno, r.Phone })
            .ToDictionaryAsync(r => r.Id);

        var head = $"**Bạn đọc đang giữ sách quá hạn** — tổng **{Num(total)}** bạn đọc" +
                   (total > top.Count ? $", hiển thị {top.Count} bạn đọc quá hạn lâu nhất" : "") + ":\n\n";
        var table = MarkdownTable(["#", "Bạn đọc", "Mã thẻ", "Điện thoại", "Số bản quá hạn", "Quá hạn lâu nhất"],
            top.Select((x, i) =>
            {
                readers.TryGetValue(x.ReaderId, out var r);
                return new[]
                {
                    (i + 1).ToString(), r != null ? $"{r.FirstName} {r.LastName}".Trim() : $"#{x.ReaderId}", r?.Cardno, r?.Phone,
                    x.Count.ToString(), x.MinDue is { } d ? $"{(today - d.Date).Days} ngày" : ""
                };
            }));
        return head + table + CirculationReportLink;
    }

    /// <summary>Sách in mượn nhiều: đếm cả phiếu đang mượn (BookOuts) lẫn đã trả (BookIns) — chỉ đếm BookOuts sẽ
    /// bỏ sót gần hết lịch sử vì phiếu đã trả không còn ở BookOuts.</summary>
    private async Task<string> ListTopBorrowedAsync(int limit, (DateTime From, DateTime ToExclusive, string Label)? period)
    {
        var loans = OpenLoans.Select(x => new { x.Barcode, x.BorrowDate, x.TenantId })
            .Concat(BookIns.Where(x => x.IsDelete != 2).Select(x => new { x.Barcode, x.BorrowDate, x.TenantId }));
        if (period is { } p)
        {
            var (f, t) = (p.From, p.ToExclusive);
            loans = loans.Where(x => x.BorrowDate >= f && x.BorrowDate < t);
        }

        var top = await (
                from x in loans
                join bc in Barcodes on new { V = x.Barcode, T = x.TenantId } equals new { V = bc.BarcodeValue, T = bc.TenantId }
                where bc.IsDelete != 2 && bc.BibId != null
                group bc by bc.BibId into g
                select new { BibId = g.Key!.Value, Count = g.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.BibId).Take(limit).ToListAsync();

        var scope = period is { } pp ? $" (trong khoảng {pp.Label})" : "";
        if (top.Count == 0) return $"Chưa có lượt mượn sách in nào{scope}.";

        var ids = top.Select(x => x.BibId).ToList();
        var bibs = await db.BibXmls.Where(x => ids.Contains(x.BibId)).Select(x => new { x.BibId, x.Title, x.Author }).ToDictionaryAsync(x => x.BibId);
        var table = MarkdownTable(["#", "Nhan đề", "Tác giả", "Lượt mượn"],
            top.Select((x, i) =>
            {
                bibs.TryGetValue(x.BibId, out var b);
                return new[] { (i + 1).ToString(), b?.Title ?? $"Biểu ghi #{x.BibId}", AuthorName(b?.Author), x.Count.ToString("N0", Vi) };
            }));
        return $"**Top {top.Count} sách in được mượn nhiều nhất**{scope}:\n\n" + table + CirculationReportLink;
    }

    /// <summary>Tài liệu số đọc nhiều: không nêu kỳ → xếp theo tổng lượt xem (TotalView); có kỳ → theo lượt mượn đọc
    /// trong kỳ (lượt xem không lưu theo ngày).</summary>
    private async Task<string> ListTopEbooksAsync(int limit, (DateTime From, DateTime ToExclusive, string Label)? period)
    {
        if (period is { } p)
        {
            var (f, t) = (p.From, p.ToExclusive);
            var top = await EbookItemLoans.Where(x => x.IsDelete != 2 && x.CheckedOutAt >= f && x.CheckedOutAt < t)
                .GroupBy(x => x.EbookItemId).Select(g => new { Id = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).ThenBy(x => x.Id).Take(limit).ToListAsync();
            if (top.Count == 0) return $"Chưa có lượt mượn đọc tài liệu số nào trong khoảng {p.Label}.";
            var titles = await EbookTitlesAsync(top.Select(x => x.Id).ToList());
            return $"**Top {top.Count} tài liệu số được mượn đọc nhiều nhất** (trong khoảng {p.Label}):\n\n" +
                   MarkdownTable(["#", "Nhan đề", "Tác giả", "Lượt mượn đọc"],
                       top.Select((x, i) =>
                       {
                           titles.TryGetValue(x.Id, out var tt);
                           return new[] { (i + 1).ToString(), tt.Title ?? $"Tài liệu #{x.Id}", tt.Author, x.Count.ToString("N0", Vi) };
                       }));
        }

        var items = await EbookItems.Where(x => x.IsDelete != 2 && x.TotalView > 0)
            .OrderByDescending(x => x.TotalView).ThenBy(x => x.Id).Take(limit)
            .Select(x => new { x.Id, x.TotalView }).ToListAsync();
        if (items.Count == 0) return "Chưa ghi nhận lượt xem tài liệu số nào.";
        var ids = items.Select(x => x.Id).ToList();
        var loanCounts = await EbookItemLoans.Where(x => x.IsDelete != 2 && ids.Contains(x.EbookItemId))
            .GroupBy(x => x.EbookItemId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count);
        var names = await EbookTitlesAsync(ids);
        return $"**Top {items.Count} tài liệu số được xem nhiều nhất** (từ trước đến nay):\n\n" +
               MarkdownTable(["#", "Nhan đề", "Tác giả", "Lượt xem", "Lượt mượn đọc"],
                   items.Select((x, i) =>
                   {
                       names.TryGetValue(x.Id, out var tt);
                       loanCounts.TryGetValue(x.Id, out var lc);
                       return new[] { (i + 1).ToString(), tt.Title ?? $"Tài liệu #{x.Id}", tt.Author, (x.TotalView ?? 0).ToString("N0", Vi), lc.ToString("N0", Vi) };
                   }));
    }

    private async Task<Dictionary<long, (string? Title, string? Author)>> EbookTitlesAsync(List<long> ids) =>
        (await db.EbookItemXmls.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Title, x.Author }).ToListAsync())
            .ToDictionary(x => x.Id, x => (x.Title, x.Author));

    public static string MarkdownTable(string[] headers, IEnumerable<string?[]> rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("| ").Append(string.Join(" | ", headers)).Append(" |\n");
        sb.Append('|').Append(string.Concat(headers.Select(_ => " --- |"))).Append('\n');
        foreach (var r in rows) sb.Append("| ").Append(string.Join(" | ", r.Select(MarkdownCell))).Append(" |\n");
        return sb.ToString().TrimEnd('\n');
    }

    /// <summary>Ô bảng: gộp xuống dòng, thoát ký tự Markdown, bỏ dấu phân cách MARC cuối nhan đề (" /", " :"), cắt ngắn.</summary>
    public static string MarkdownCell(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var v = System.Text.RegularExpressions.Regex.Replace(value, @"\s+", " ").Trim().TrimEnd('/', ':', ';', ',', '=', ' ');
        if (v.Length > 80) v = v[..77].TrimEnd() + "…";
        return v.Replace("\\", "\\\\").Replace("|", "\\|").Replace("*", "\\*").Replace("_", "\\_");
    }

    private static readonly System.Globalization.CultureInfo Vi = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
    private static string Num(int value) => value.ToString("N0", Vi);

    /// <summary>Tác giả MARC dạng đảo, nhiều người nối bằng dấu phẩy ("Nguyễn, Thế Hoàn,Trần, Văn Nhung") →
    /// "Nguyễn Thế Hoàn; Trần Văn Nhung". Tên tập thể (không có dấu phẩy) giữ nguyên.</summary>
    public static string? AuthorName(string? author)
    {
        if (string.IsNullOrWhiteSpace(author)) return author;
        var v = System.Text.RegularExpressions.Regex.Replace(author, @"(\p{L}+), (?=\p{L})", "$1 ");
        return System.Text.RegularExpressions.Regex.Replace(v, @"\s*,\s*", "; ");
    }

    private static string ReaderLabel(string? name, string? cardNo)
    {
        var n = (name ?? "").Trim();
        return string.IsNullOrEmpty(cardNo) ? n : n.Length == 0 ? cardNo : $"{n} ({cardNo})";
    }

    // Dữ liệu nghiệp vụ lưu theo giờ thư viện (LibraryClock) nên hiển thị thẳng, không đổi múi.
    private static string FormatDate(DateTime? value) => value is { } d ? d.Date.ToString("dd/MM/yyyy") : "";

    private static string VietnameseWeekday(DateTime d) => d.DayOfWeek switch
    {
        DayOfWeek.Monday => "thứ Hai", DayOfWeek.Tuesday => "thứ Ba", DayOfWeek.Wednesday => "thứ Tư",
        DayOfWeek.Thursday => "thứ Năm", DayOfWeek.Friday => "thứ Sáu", DayOfWeek.Saturday => "thứ Bảy", _ => "Chủ nhật"
    };

    private static CancellationTokenSource LinkedTimeout(CancellationToken ct)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(LlmTimeout);
        return cts;
    }

    private static object[] BuildToolsPayload() =>
    [
        new
        {
            functionDeclarations = new object[]
            {
                new
                {
                    name = PeriodStatsTool,
                    description = "Lấy số liệu hoạt động thư viện TRONG MỘT KHOẢNG THỜI GIAN: lượt mượn đọc tài liệu số, lượt mượn/trả " +
                                  "sách in, lượt trả quá hạn, phiếu phạt và tiền phạt, lượt vào thư viện, bạn đọc mới, đơn đặt mua, " +
                                  "chứng từ nhập kho. Dùng cho mọi câu hỏi có mốc thời gian: hôm nay, hôm qua, tuần này, tuần trước, " +
                                  "tháng này, tháng 8, quý 3, năm nay, năm 2025, từ ngày… đến ngày….",
                    parameters = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            fromDate = new { type = "STRING", description = "Ngày bắt đầu (yyyy-MM-dd), tự tính từ ngày hôm nay. 'Hôm nay' → fromDate = toDate = hôm nay." },
                            toDate   = new { type = "STRING", description = "Ngày kết thúc, bao gồm (yyyy-MM-dd)." },
                            label    = new { type = "STRING", description = "Mô tả ngắn khoảng thời gian, vd 'hôm nay', 'tháng 8/2026', 'quý 3 năm 2026'." }
                        },
                        required = new[] { "fromDate", "toDate" }
                    }
                },
                new
                {
                    name = ListTool,
                    description = "Liệt kê DANH SÁCH chi tiết (không phải con số tổng): tài liệu đang mượn, tài liệu đang mượn quá hạn, " +
                                  "bạn đọc đang có sách quá hạn, tài liệu sách in được mượn nhiều nhất, tài liệu số được đọc/mượn nhiều nhất.",
                    parameters = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            listType = new
                            {
                                type = "STRING",
                                @enum = new[] { "current_loans", "overdue_loans", "overdue_readers", "top_borrowed", "top_ebooks" },
                                description = "current_loans = tài liệu đang mượn; overdue_loans = tài liệu đang mượn quá hạn; " +
                                              "overdue_readers = bạn đọc đang có sách quá hạn; top_borrowed = sách in mượn nhiều; " +
                                              "top_ebooks = tài liệu số đọc/mượn nhiều."
                            },
                            limit    = new { type = "INTEGER", description = $"Số dòng muốn xem (mặc định {ListDefaultSize}, tối đa {ListMaxSize})." },
                            fromDate = new { type = "STRING", description = "Tuỳ chọn (yyyy-MM-dd): chỉ tính các lượt mượn từ ngày này — dùng khi người dùng nêu khoảng thời gian (vd 'mượn nhiều tháng này')." },
                            toDate   = new { type = "STRING", description = "Tuỳ chọn (yyyy-MM-dd): đến ngày này (bao gồm)." },
                            label    = new { type = "STRING", description = "Mô tả ngắn khoảng thời gian nếu có, vd 'tháng này'." }
                        },
                        required = new[] { "listType" }
                    }
                }
            }
        }
    ];

}
