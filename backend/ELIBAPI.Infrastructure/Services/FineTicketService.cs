using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>
/// Phiếu phạt lưu thông (port ELIB-LRC 09-30 / 10-03 / 10-04, xem <see cref="IFineTicketService"/>).
///
/// Đa đơn vị — khác LRC (đã bỏ TenantId):
/// <list type="bullet">
/// <item><c>tenantId</c> = đơn vị JWT; null = tài khoản hệ thống (không giới hạn). Phiếu/bạn đọc khác đơn vị → 404.</item>
/// <item>Phiếu mới gắn đơn vị của BẠN ĐỌC (tài khoản hệ thống lập phiếu cũng không tạo phiếu "mồ côi" TenantId=null);
/// dòng phạt gắn đơn vị của phiếu, lượt trả (BookIn) gắn đơn vị của phiếu mượn.</item>
/// <item>ĐKCB chỉ duy nhất trong 1 đơn vị → tra theo (mã, đơn vị) qua <see cref="BarcodeTenantLookup"/>.</item>
/// <item>Lý do phạt / trạng thái ĐKCB / chính sách lưu thông: của đơn vị phiếu hoặc dùng chung (TenantId=null),
/// ưu tiên bản của đơn vị khi trùng mã.</item>
/// </list>
/// </summary>
public class FineTicketService(
    ELIBAPIDbContext db,
    IGenericRepository<CFineTicket, CFineTicketSearchRequest, CFineTicketRequest> repo) : IFineTicketService
{
    private const string ReturnedStatus  = PrintLoans.ReturnedStatus;
    private const int    OpenStatus      = 1;   // CFineTicket.Status: 1 = đang xử lý, 2 = đã hoàn thành
    private const int    DoneStatus      = 2;
    private const int    OwesDocumentYes = 2;
    private const string OverdueCode     = "QUAHAN";
    private const string LostCode        = "MATTL";   // C_Fine_type "Mất tài liệu" — mặc định cho phiếu tích chọn chưa quá hạn
    private const string TicketNotFound  = "Không tìm thấy phiếu phạt";
    private const string ReaderNotFound  = "Không tìm thấy bạn đọc";

    // ── Danh sách (repo đã lọc đơn vị) ───────────────────────────────────────

    public async Task<PagedResult<FineTicketListRow>> SearchAsync(CFineTicketSearchRequest request)
    {
        var page = await repo.SearchAsync(request);
        var tickets = page.Items;
        var readerIds = tickets.Where(x => x.ReaderId.HasValue).Select(x => x.ReaderId!.Value).Distinct().ToList();
        var readers   = await db.Readers.Where(x => readerIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Cardno, x.FirstName, x.LastName }).ToDictionaryAsync(x => x.Id);
        var typeIds   = tickets.Where(x => x.FineTypeId.HasValue).Select(x => x.FineTypeId!.Value).Distinct().ToList();
        var typeNames = await db.CFineTypes.Where(x => typeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);

        return new PagedResult<FineTicketListRow>
        {
            Items = tickets.Select(t =>
            {
                var reader = t.ReaderId.HasValue && readers.TryGetValue(t.ReaderId.Value, out var rd) ? rd : null;
                return new FineTicketListRow(t.Id, t.PublicId, t.Code, t.FineDate, t.Status, reader?.Cardno,
                    reader != null ? $"{reader.FirstName} {reader.LastName}".Trim() : null,
                    t.FineTypeId, t.FineTypeId.HasValue && typeNames.TryGetValue(t.FineTypeId.Value, out var tn) ? tn : null,
                    t.TotalAmount, t.PaidAmount, Remaining(t), t.TenantName);
            }).ToList(),
            TotalCount = page.TotalCount,
            PageIndex  = page.PageIndex,
            PageSize   = page.PageSize
        };
    }

    /// <summary>Tổng Phải thu / Đã thu / Còn lại trên TOÀN BỘ kết quả khớp bộ lọc (không chỉ trang hiện tại).</summary>
    public async Task<FineTicketTotals> TotalsAsync(CFineTicketSearchRequest request)
    {
        var all = await repo.SearchAllAsync(request);
        var receivable = all.Sum(x => (x.TotalAmount ?? 0) - (x.DiscountAmount ?? 0));
        var received   = all.Sum(x => x.PaidAmount ?? 0);
        return new FineTicketTotals(receivable, received, receivable - received);
    }

    // ── Lập / gom / lưu phiếu ────────────────────────────────────────────────

    /// <summary>Phiếu phạt thủ công (không có dòng tài liệu) — nút "Thêm mới" trên trang danh sách.</summary>
    public async Task<ServiceResult<FineTicketDetail>> CreateAsync(CreateFineTicketRequest r, long? userId, long? tenantId)
    {
        var reader = await Readers(tenantId).FirstOrDefaultAsync(x => x.Id == r.ReaderId);
        if (reader == null) return ServiceResult<FineTicketDetail>.NotFound(ReaderNotFound);

        var ticketTenant = reader.TenantId ?? tenantId;
        var rates = await ResolveFineRatesAsync((int?)reader.ReaderTypeId, null, ticketTenant);
        var now = LibraryClock.Now;
        var ticket = new CFineTicket
        {
            ReaderId       = r.ReaderId,
            FineDate       = r.FineDate ?? now,
            Status         = r.Status ?? OpenStatus,
            Lanphat        = await NextFineRoundAsync(r.ReaderId),
            DiscountAmount = r.DiscountAmount,
            PaidAmount     = r.PaidAmount,
            TotalAmount    = ComputeTotal([], r.OwesDocument, rates, r.TotalAmount),
            OwesDocument   = r.OwesDocument,
            FineTypeId     = r.FineTypeId,
            FineMethodId   = r.FineMethodId,
            Note           = r.Note,
            PublicId       = Guid.NewGuid(),
            TenantId       = ticketTenant,
            CreatedRowBy   = userId,
            CreatedRowDate = now
        };
        await AddWithCodeAsync(ticket);
        return ServiceResult<FineTicketDetail>.Ok(await BuildDetailAsync(ticket, rates));
    }

    /// <summary>Gom tài liệu ĐANG MƯỢN quá hạn của bạn đọc (+ các phiếu mượn cán bộ tích chọn, vd mất tài liệu) vào 1 phiếu
    /// phạt đang mở (tạo mới nếu chưa có). Phiếu mượn đã trả (Status "R") không bị phạt dù hạn trả đã qua — trước đây
    /// K12 lọc Status == "O" nên bỏ sót phiếu cũ lưu "1".</summary>
    public async Task<ServiceResult<FineTicketDetail>> BuildForReaderAsync(BuildFineTicketRequest r, long? userId, long? tenantId)
    {
        var reader = await Readers(tenantId).FirstOrDefaultAsync(x => x.Id == r.ReaderId);
        if (reader == null) return ServiceResult<FineTicketDetail>.NotFound(ReaderNotFound);

        var now = LibraryClock.Now;
        var ticket = await Tickets(tenantId).FirstOrDefaultAsync(x => x.ReaderId == r.ReaderId && x.Status == OpenStatus);
        if (ticket == null)
        {
            ticket = new CFineTicket
            {
                ReaderId       = r.ReaderId,
                FineDate       = now,
                Status         = OpenStatus,
                Lanphat        = await NextFineRoundAsync(r.ReaderId),
                PublicId       = Guid.NewGuid(),
                TenantId       = reader.TenantId ?? tenantId,
                CreatedRowBy   = userId,
                CreatedRowDate = now
            };
            await AddWithCodeAsync(ticket);
        }

        var rates = await ResolveFineRatesAsync((int?)reader.ReaderTypeId, r.CircPlaceId, ticket.TenantId);
        var finedLoanIds = await db.CFines.Where(x => x.TicketId == ticket.Id && x.IsDelete != 2 && x.Borrow_Id != null)
            .Select(x => x.Borrow_Id!.Value).ToListAsync();

        // Đang mượn = Status khác "R" (dữ liệu cũ lưu "1"/"O"); chưa có dòng phạt trong phiếu này. Quá hạn → dòng
        // "Quá hạn" tính theo ngày; phiếu cán bộ tích chọn chưa quá hạn → dòng "Mất tài liệu" 0đ để cán bộ nhập tiền.
        var selectedIds = r.LoanIds ?? [];
        var loans = await db.BookOuts.Where(x => x.ReaderId == r.ReaderId && x.IsDelete != 2 && x.Status != ReturnedStatus
                                                 && !finedLoanIds.Contains(x.Id)
                                                 && ((x.DueDate.HasValue && x.DueDate < now) || selectedIds.Contains(x.Id)))
            .ToListAsync();
        var lostCode = loans.Any(l => !IsOverdue(l, now))
            ? await FineTypes(ticket.TenantId).Where(x => x.Code == LostCode).Select(x => x.Code).FirstOrDefaultAsync()
            : null;

        // Biểu ghi của các ĐKCB: 1 truy vấn cho cả lô, khoá (mã, đơn vị) — 2 đơn vị có thể cùng mã ĐKCB.
        var codes = loans.Where(l => !string.IsNullOrEmpty(l.Barcode)).Select(l => l.Barcode!).Distinct().ToList();
        var bibByCode = codes.Count == 0 ? [] : (await db.Barcodes
                .Where(b => b.BarcodeValue != null && codes.Contains(b.BarcodeValue) && b.IsDelete != 2)
                .OrderBy(b => b.Id).Select(b => new { b.BarcodeValue, b.TenantId, b.BibId }).ToListAsync())
            .GroupBy(b => (b.BarcodeValue!, b.TenantId ?? 0)).ToDictionary(g => g.Key, g => g.First().BibId);

        foreach (var loan in loans)
        {
            var overdue = IsOverdue(loan, now);
            var amount = overdue ? (int)Math.Max(0, (now.Date - loan.DueDate!.Value.Date).TotalDays) * rates.OverdueRate : 0;
            db.CFines.Add(new CFine
            {
                ReaderId       = r.ReaderId,
                FineDate       = now,
                Fine_type_id   = overdue ? OverdueCode : lostCode,
                Value          = amount,
                Borrow_Id      = loan.Id,
                BorrowDate     = loan.BorrowDate,
                Barcode        = loan.Barcode,
                Bibid          = loan.Barcode != null && bibByCode.TryGetValue((loan.Barcode, loan.TenantId ?? 0), out var bibId) ? bibId : null,
                TicketId       = ticket.Id,
                PublicId       = Guid.NewGuid(),
                TenantId       = ticket.TenantId,
                CreatedRowBy   = userId,
                CreatedRowDate = now
            });
            if (!overdue) continue;
            loan.FineValue      = amount;
            loan.UpdateRowBy    = userId;
            loan.UpdatedRowDate = now;
        }
        await db.SaveChangesAsync();

        var lines = await db.CFines.Where(x => x.TicketId == ticket.Id && x.IsDelete != 2).ToListAsync();
        ticket.TotalAmount    = ComputeTotal(lines, ticket.OwesDocument, rates);
        ticket.UpdateRowBy    = userId;
        ticket.UpdatedRowDate = now;
        await db.SaveChangesAsync();

        return ServiceResult<FineTicketDetail>.Ok(await BuildDetailAsync(ticket, rates));
    }

    public async Task<ServiceResult<FineTicketDetail>> GetDetailAsync(Guid publicId, long? tenantId)
    {
        var ticket = await Tickets(tenantId).FirstOrDefaultAsync(x => x.PublicId == publicId);
        if (ticket == null) return ServiceResult<FineTicketDetail>.NotFound(TicketNotFound);
        var rates = await ResolveFineRatesAsync(await ReaderTypeOfAsync(ticket.ReaderId), null, ticket.TenantId);
        return ServiceResult<FineTicketDetail>.Ok(await BuildDetailAsync(ticket, rates));
    }

    public async Task<ServiceResult<FineTicketDetail>> SaveAsync(Guid publicId, SaveFineTicketRequest r, long? userId, long? tenantId)
    {
        var ticket = await Tickets(tenantId).FirstOrDefaultAsync(x => x.PublicId == publicId);
        if (ticket == null) return ServiceResult<FineTicketDetail>.NotFound(TicketNotFound);

        var lines = await db.CFines.Where(x => x.TicketId == ticket.Id && x.IsDelete != 2).ToListAsync();

        // Thêm / xoá dòng: kiểm tra hết trước khi ghi để lỗi thì không lưu dở.
        var newLines = (r.Lines ?? []).Where(x => x.Id <= 0).ToList();
        var deleteIds = (r.DeletedLineIds ?? []).Where(id => lines.Any(l => l.Id == id)).ToHashSet();
        var prepared = new List<CFine>();
        if (newLines.Count > 0 || deleteIds.Count > 0)
        {
            if (ticket.Status == DoneStatus)
                return ServiceResult<FineTicketDetail>.BadRequest("Phiếu đã hoàn thành, không thêm/xoá dòng phạt được.");
            var (built, error) = await PrepareNewLinesAsync(ticket, newLines, lines.Where(l => !deleteIds.Contains(l.Id)).ToList());
            if (error != null) return ServiceResult<FineTicketDetail>.BadRequest(error);
            prepared = built;
        }

        var rates = await ResolveFineRatesAsync(await ReaderTypeOfAsync(ticket.ReaderId), r.CircPlaceId, ticket.TenantId);
        var now = LibraryClock.Now;
        ticket.FineDate       = r.FineDate ?? ticket.FineDate;
        ticket.Status         = r.Status ?? ticket.Status;
        ticket.DiscountAmount = r.DiscountAmount;
        ticket.PaidAmount     = r.PaidAmount;
        ticket.OwesDocument   = r.OwesDocument;
        ticket.FineTypeId     = r.FineTypeId ?? ticket.FineTypeId;
        ticket.FineMethodId   = r.FineMethodId ?? ticket.FineMethodId;
        ticket.Note           = r.Note;

        // Xoá mềm — không hoàn tác trạng thái ĐKCB / phiếu mượn đã đóng trước đó.
        foreach (var line in lines.Where(l => deleteIds.Contains(l.Id)))
        {
            line.IsDelete       = 2;
            line.UpdateRowBy    = userId;
            line.UpdatedRowDate = now;
        }
        lines.RemoveAll(l => deleteIds.Contains(l.Id));

        foreach (var update in (r.Lines ?? []).Where(x => x.Id > 0))
        {
            var line = lines.FirstOrDefault(x => x.Id == update.Id);
            if (line == null) continue;
            if (update.Value.HasValue) line.Value = update.Value;
            var changed = update.FineTypeId != null && update.FineTypeId != line.Fine_type_id;
            if (changed) line.Fine_type_id = update.FineTypeId;
            // Dòng tích chọn được tạo sẵn với lý do "Mất tài liệu" — lý do không đổi nhưng phiếu mượn còn mở thì vẫn áp.
            if (changed || await LoanOpenAsync(line.Borrow_Id)) await ApplyReasonStatusAsync(ticket, line, userId, now);
            line.UpdateRowBy    = userId;
            line.UpdatedRowDate = now;
        }

        // Dòng mới: lý do có "Trạng thái" (vd Mất) thì đổi trạng thái ĐKCB + đóng phiếu mượn đang mở, như sửa lý do.
        foreach (var line in prepared)
        {
            line.FineDate = now; line.CreatedRowBy = userId; line.CreatedRowDate = now;
            db.CFines.Add(line);
            lines.Add(line);
            await ApplyReasonStatusAsync(ticket, line, userId, now);
        }

        ticket.TotalAmount    = ComputeTotal(lines, ticket.OwesDocument, rates, r.TotalAmount ?? ticket.TotalAmount);
        ticket.UpdateRowBy    = userId;
        ticket.UpdatedRowDate = now;
        await db.SaveChangesAsync();

        return ServiceResult<FineTicketDetail>.Ok(await BuildDetailAsync(ticket, rates));
    }

    /// <summary>Lý do phạt của dòng có "Trạng thái" (C_Fine_type.Status_Reg_Id → Barcode_Status), khi lý do vừa đổi hoặc
    /// phiếu mượn còn mở: đặt trạng thái ĐKCB theo mã đó và đóng phiếu mượn còn mở (BookOut "R" + 1 BookIn, như trả ở
    /// quầy). Phiếu mượn đã đóng thì lý do không đổi không áp lại. Status_Reg_Id thường là mã ("L"), nhưng trang danh mục
    /// từng lưu PublicId — đọc được cả hai.</summary>
    private async Task ApplyReasonStatusAsync(CFineTicket ticket, CFine line, long? userId, DateTime now)
    {
        var statusRef = await FineTypes(ticket.TenantId).Where(x => x.Code == line.Fine_type_id)
            .Select(x => x.Status_Reg_Id).FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(statusRef)) return;
        statusRef = statusRef.Trim();
        var statusCode = (await db.BarcodeStatuses
                .Where(x => x.IsDelete != 2 && (ticket.TenantId == null || x.TenantId == ticket.TenantId || x.TenantId == null))
                .Select(x => new { x.Id, x.PublicId }).ToListAsync())
            .FirstOrDefault(x => x.Id == statusRef || x.PublicId.ToString().Equals(statusRef, StringComparison.OrdinalIgnoreCase))?.Id;
        if (statusCode == null) return;

        var loan = line.Borrow_Id.HasValue
            ? await db.BookOuts.FirstOrDefaultAsync(x => x.Id == line.Borrow_Id && x.Status != ReturnedStatus && x.IsDelete != 2)
            : null;

        if (!string.IsNullOrEmpty(line.Barcode))
        {
            var barcode = await BarcodeTenantLookup.ForTransactionAsync(db, loan?.Reg_Seq_Id, line.Barcode, loan?.TenantId ?? ticket.TenantId);
            if (barcode != null)
            {
                barcode.Status         = statusCode;
                barcode.UpdateRowBy    = userId;
                barcode.UpdatedRowDate = now;
            }
        }

        if (loan == null) return;
        loan.Status         = ReturnedStatus;
        loan.UpdateRowBy    = userId;
        loan.UpdatedRowDate = now;
        db.BookIns.Add(new BookIn
        {
            ReaderId       = loan.ReaderId,
            Barcode        = loan.Barcode,
            BorrowDate     = loan.BorrowDate,
            DueDate        = loan.DueDate,
            ReturnDate     = now,
            BookOutId      = loan.Id,
            Renew          = loan.Renew,
            CircPlace      = loan.CircPlace,
            StoreId        = loan.Store,
            FineValue      = line.Value,
            UserId         = (int?)userId,
            TenantId       = loan.TenantId,
            CreatedRowBy   = userId,
            CreatedRowDate = now,
            PublicId       = Guid.NewGuid()
        });
    }

    /// <summary>Dựng các dòng phạt thêm tay (chưa ghi). Lý do bắt buộc và phải có trong danh mục (của đơn vị hoặc dùng
    /// chung); số tiền không âm; ĐKCB không bắt buộc — có thì phải tồn tại TRONG ĐƠN VỊ của phiếu, gắn biểu ghi và phiếu
    /// mượn ĐANG MỞ của chính bạn đọc (nếu có). Không cho 2 dòng cùng ĐKCB + cùng lý do trong phiếu.</summary>
    private async Task<(List<CFine> Lines, string? Error)> PrepareNewLinesAsync(CFineTicket ticket, List<FineTicketLineUpdate> input, List<CFine> existing)
    {
        var result = new List<CFine>();
        if (input.Count == 0) return (result, null);
        var codes = await FineTypes(ticket.TenantId).Where(x => x.Code != null).Select(x => x.Code!).ToListAsync();
        var taken = existing.Select(l => (Barcode: l.Barcode?.Trim().ToLowerInvariant() ?? "", l.Fine_type_id)).ToList();
        foreach (var item in input)
        {
            var code = item.FineTypeId?.Trim();
            if (string.IsNullOrEmpty(code) || !codes.Contains(code)) return ([], "Dòng phạt mới phải chọn lý do phạt hợp lệ.");
            if (item.Value is < 0) return ([], "Số tiền phạt không được âm.");
            var line = new CFine
            {
                ReaderId = ticket.ReaderId, TicketId = ticket.Id, Fine_type_id = code, Value = item.Value ?? 0,
                PublicId = Guid.NewGuid(), TenantId = ticket.TenantId,
            };
            var barcode = item.Barcode?.Trim();
            if (!string.IsNullOrEmpty(barcode))
            {
                // Không phân biệt hoa/thường — ĐKCB thường được gõ tay. Phiếu không có đơn vị (dữ liệu cũ) mà mã trùng ở
                // nhiều đơn vị → báo lỗi thay vì lấy bừa.
                var (copy, ambiguous) = await BarcodeTenantLookup.ByValueAsync(db.Barcodes, barcode, ticket.TenantId, normalize: true);
                if (ambiguous) return ([], BarcodeTenantLookup.AmbiguousMessage);
                if (copy == null) return ([], $"Không tìm thấy ĐKCB \"{barcode}\".");
                var key = (copy.BarcodeValue!.ToLowerInvariant(), (string?)code);
                if (taken.Any(t => t.Barcode == key.Item1 && t.Fine_type_id == code))
                    return ([], $"ĐKCB \"{copy.BarcodeValue}\" đã có dòng phạt cùng lý do trong phiếu.");
                taken.Add(key);
                var loan = await db.BookOuts.Where(x => x.ReaderId == ticket.ReaderId && x.Barcode == copy.BarcodeValue
                                                        && (x.TenantId ?? 0) == (copy.TenantId ?? 0)
                                                        && x.Status != ReturnedStatus && x.IsDelete != 2)
                    .OrderByDescending(x => x.BorrowDate).Select(x => new { x.Id, x.BorrowDate }).FirstOrDefaultAsync();
                line.Barcode    = copy.BarcodeValue;
                line.Bibid      = copy.BibId;
                line.Borrow_Id  = loan?.Id;
                line.BorrowDate = loan?.BorrowDate;
            }
            result.Add(line);
        }
        return (result, null);
    }

    private async Task<bool> LoanOpenAsync(long? borrowId) =>
        borrowId.HasValue && await db.BookOuts.AnyAsync(x => x.Id == borrowId && x.Status != ReturnedStatus && x.IsDelete != 2);

    private static bool IsOverdue(BookOut loan, DateTime now) => loan.DueDate.HasValue && loan.DueDate < now;

    // ── Phạm vi đơn vị ───────────────────────────────────────────────────────

    private IQueryable<Core.Entities.Dbo.Reader> Readers(long? tenantId) =>
        db.Readers.Where(x => x.IsDelete != 2 && (!tenantId.HasValue || x.TenantId == tenantId));

    private IQueryable<CFineTicket> Tickets(long? tenantId) =>
        db.CFineTickets.Where(x => x.IsDelete != 2 && (!tenantId.HasValue || x.TenantId == tenantId));

    /// <summary>Lý do phạt của đơn vị phiếu + dùng chung; trùng mã thì bản của đơn vị đứng trước.</summary>
    private IQueryable<CFineType> FineTypes(long? tenantId) =>
        db.CFineTypes.Where(x => x.IsDelete != 2 && (tenantId == null || x.TenantId == tenantId || x.TenantId == null))
            .OrderByDescending(x => x.TenantId != null);

    // ── Tính tiền ────────────────────────────────────────────────────────────

    /// <summary>Đơn giá 3 khoản phí theo chính sách lưu thông của bạn đọc (loại bạn đọc + điểm lưu thông; không có thì
    /// chính sách chung của điểm lưu thông), của đơn vị phiếu hoặc dùng chung — ưu tiên của đơn vị.</summary>
    private async Task<FineFeeLegend> ResolveFineRatesAsync(int? readerTypeId, int? circPlaceId, long? tenantId)
    {
        var policies = db.PolicyCircs.Where(x => x.IsDelete != 2 && x.CircPlace == circPlaceId
                                                 && (tenantId == null || x.TenantId == tenantId || x.TenantId == null))
            .OrderByDescending(x => x.TenantId != null);
        var policy = await policies.FirstOrDefaultAsync(x => x.ReaderType == readerTypeId)
                  ?? await policies.FirstOrDefaultAsync(x => x.ReaderType == null);
        if (policy == null) return new FineFeeLegend(0, 0, 0);

        var rates = await db.PolicyCircFines
            .Where(x => x.PolicyCircId == policy.Id && x.IsDelete != 2)
            .Join(db.CFineTypes.Where(t => t.IsDelete != 2), pf => pf.FineTypeId, t => t.Id, (pf, t) => new { t.Code, pf.FineAmount })
            .ToListAsync();
        double Rate(string code) => rates.FirstOrDefault(x => x.Code == code)?.FineAmount ?? 0;
        return new FineFeeLegend(Rate("QUAHAN"), Rate("XLKY"), Rate("VANCHUYEN"));
    }

    /// <summary>Phiếu có dòng tài liệu tính tổng từ các dòng; phiếu thủ công (không có dòng) dùng số tiền cán bộ nhập.
    /// Nợ tài liệu thì cộng thêm phí xử lý kỹ thuật + vận chuyển.</summary>
    public static double ComputeTotal(IReadOnlyCollection<CFine> lines, int? owesDocument, FineFeeLegend rates, double? manualAmount = null)
    {
        var total = lines.Count > 0 ? lines.Sum(x => x.Value ?? 0) : (manualAmount ?? 0);
        if (owesDocument == OwesDocumentYes) total += rates.TechFee + rates.ShippingFee;
        return total;
    }

    private static double Remaining(CFineTicket t) => (t.TotalAmount ?? 0) - (t.DiscountAmount ?? 0) - (t.PaidAmount ?? 0);

    private async Task<int> NextFineRoundAsync(long? readerId) =>
        await db.CFineTickets.CountAsync(x => x.ReaderId == readerId && x.IsDelete != 2) + 1;

    private async Task<int?> ReaderTypeOfAsync(long? readerId) =>
        readerId.HasValue ? (int?)await db.Readers.Where(x => x.Id == readerId).Select(x => x.ReaderTypeId).FirstOrDefaultAsync() : null;

    /// <summary>Lưu phiếu mới rồi gán số phiếu "PT{Id:D3}" (cần Id nên phải lưu 2 lần).</summary>
    private async Task AddWithCodeAsync(CFineTicket ticket)
    {
        db.CFineTickets.Add(ticket);
        await db.SaveChangesAsync();
        ticket.Code = $"PT{ticket.Id:D3}";
        await db.SaveChangesAsync();
    }

    private async Task<FineTicketDetail> BuildDetailAsync(CFineTicket ticket, FineFeeLegend rates)
    {
        var reader = ticket.ReaderId.HasValue
            ? await db.Readers.Where(x => x.Id == ticket.ReaderId).Select(x => new { x.Cardno, x.FirstName, x.LastName, x.IssueDate }).FirstOrDefaultAsync()
            : null;
        var fineTypeName = ticket.FineTypeId.HasValue
            ? await db.CFineTypes.Where(x => x.Id == ticket.FineTypeId).Select(x => x.Name).FirstOrDefaultAsync() : null;
        var fineMethodName = ticket.FineMethodId.HasValue
            ? await db.CFineMethods.Where(x => x.Id == ticket.FineMethodId).Select(x => x.Name).FirstOrDefaultAsync() : null;

        var lines = await db.CFines.Where(x => x.TicketId == ticket.Id && x.IsDelete != 2).OrderBy(x => x.Id).ToListAsync();
        var bibIds = lines.Where(x => x.Bibid.HasValue).Select(x => x.Bibid!.Value).Distinct().ToList();
        var titles = await db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId, x => x.Title);
        var borrowIds = lines.Where(x => x.Borrow_Id.HasValue).Select(x => x.Borrow_Id!.Value).ToList();
        var dueDates = await db.BookOuts.Where(x => borrowIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DueDate);
        var now = LibraryClock.Now;

        var items = lines.Select(l => new FineTicketLine(
            l.Id, l.Barcode,
            l.Bibid.HasValue && titles.TryGetValue(l.Bibid.Value, out var t) ? t : null,
            l.Fine_type_id,
            l.Borrow_Id.HasValue && dueDates.TryGetValue(l.Borrow_Id.Value, out var due) && due.HasValue
                ? (int)Math.Max(0, ((l.FineDate ?? now).Date - due.Value.Date).TotalDays) : 0,
            rates.OverdueRate,
            l.Value)).ToList();

        return new FineTicketDetail(
            ticket.Id, ticket.PublicId, ticket.Code, ticket.ReaderId, reader?.Cardno,
            reader != null ? $"{reader.FirstName} {reader.LastName}".Trim() : null,
            ticket.FineDate, ticket.Status, ticket.Lanphat, ticket.DiscountAmount, ticket.PaidAmount, ticket.TotalAmount,
            Remaining(ticket), ticket.OwesDocument, ticket.FineTypeId, fineTypeName, ticket.FineMethodId, fineMethodName,
            reader?.IssueDate, ticket.Note, items, rates);
    }
}
