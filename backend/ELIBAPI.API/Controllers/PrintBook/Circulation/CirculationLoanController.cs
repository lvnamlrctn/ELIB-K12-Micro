using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Circulation/Loan")]
[Authorize]
public class CirculationLoanController(ELIBAPIDbContext db, ISystemParameterService sysParam) : ControllerBase
{
    [HttpPost("ReaderSnapshot")]
    [Permission("BORROW", "view")]
    public async Task<IActionResult> ReaderSnapshot([FromBody] ReaderSnapshotRequest r)
    {
        var tenantId = GetTenantId();
        var cardNo = (r.CardNo ?? "").Trim().ToLower();
        var reader = await db.Readers.FirstOrDefaultAsync(x => x.Cardno!.Trim().ToLower() == cardNo && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (reader == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy bạn đọc"));

        var readerTypeName = reader.ReaderTypeId.HasValue
            ? await db.ReaderTypes.Where(x => x.Id == reader.ReaderTypeId).Select(x => x.Name).FirstOrDefaultAsync()
            : null;

        // Lớp / khóa học / đơn vị — hiển thị ở màn hình mượn trả để cán bộ đối chiếu bạn đọc.
        var className = reader.ClassId.HasValue
            ? await db.Classes.Where(x => x.Id == reader.ClassId).Select(x => x.Name).FirstOrDefaultAsync()
            : null;
        var courseName = reader.CourseId.HasValue
            ? await db.Courses.Where(x => x.Id == reader.CourseId).Select(x => x.Name).FirstOrDefaultAsync()
            : null;
        var orgName = reader.OrgId.HasValue
            ? await db.Orgs.Where(x => x.Id == reader.OrgId).Select(x => x.Name).FirstOrDefaultAsync()
            : null;

        var loans = await db.BookOuts
            .Where(x => x.ReaderId == reader.Id && x.Status != "R" && x.IsDelete != 2)
            .OrderByDescending(x => x.BorrowDate)
            .ToListAsync();

        var barcodeMap = new Dictionary<string, string>();
        foreach (var loan in loans.Where(l => l.Barcode != null))
        {
            var bc = await BarcodeTenantLookup.ForTransactionAsync(db, loan.Reg_Seq_Id, loan.Barcode, loan.TenantId);
            if (bc?.BibId != null)
            {
                var xml = await db.BibXmls.FirstOrDefaultAsync(x => x.BibId == bc.BibId);
                barcodeMap[loan.Barcode!] = xml?.Title ?? "";
            }
        }

        var storeIds = loans.Where(l => l.Store.HasValue).Select(l => l.Store!.Value).Distinct().ToList();
        var storeNames = storeIds.Count > 0
            ? await db.Stores.Where(x => storeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name)
            : new Dictionary<long, string?>();

        var today = DateTime.Today;
        var isLocked   = reader.Status != 2;
        var isExpired  = reader.ExpireDate.HasValue && reader.ExpireDate < today;
        var hasOverdue = loans.Any(l => l.Status == "O" && l.DueDate.HasValue && l.DueDate < DateTime.Now);
        var canBorrow  = !isLocked && !isExpired && !hasOverdue;
        string? blockReason = isLocked
            ? "Bạn đọc không được phép mượn tài liệu (thẻ bị khoá)"
            : isExpired
                ? "Bạn đọc không được phép mượn tài liệu (thẻ đã hết hạn)"
                : hasOverdue
                    ? "Bạn đọc không được phép mượn tài liệu (đang có tài liệu quá hạn)"
                    : null;

        var snapshot = new LoanReaderSnapshotResponse
        {
            ReaderId   = reader.Id,
            ReaderPublicId = reader.PublicId,
            CardNo     = reader.Cardno,
            FullName   = $"{reader.FirstName} {reader.LastName}".Trim(),
            ReaderType = readerTypeName,
            ClassName  = className,
            CourseName = courseName,
            OrgName    = orgName,
            CitizenId  = reader.CitizenId,
            ExpireDate = reader.ExpireDate?.ToString("yyyy-MM-dd"),
            IssueDate  = reader.IssueDate?.ToString("yyyy-MM-dd"),
            Photo      = reader.Photo,
            Balance    = reader.Blane,
            Status     = reader.Status,
            IsLocked    = isLocked,
            IsExpired   = isExpired,
            HasOverdue  = hasOverdue,
            CanBorrow   = canBorrow,
            BlockReason = blockReason,
            CurrentLoans = loans.Select(l => new CurrentLoanItem
            {
                Id         = l.Id,
                Barcode    = l.Barcode,
                BibTitle   = l.Barcode != null && barcodeMap.TryGetValue(l.Barcode, out var t) ? t : null,
                BorrowDate = l.BorrowDate,
                DueDate    = l.DueDate,
                Status     = l.Status,
                FineValue  = l.FineValue,
                RenewCount = l.Renew,
                Location   = l.Store.HasValue && storeNames.TryGetValue(l.Store.Value, out var sn) ? sn : null,
                Note       = l.Note
            }).ToList()
        };
        return Ok(ApiResponse<LoanReaderSnapshotResponse>.Ok(snapshot));
    }

    [HttpPost("Checkout")]
    [Permission("BORROW", "add")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest r)
    {
        if (r.CircPlaceId is null || r.CircPlaceId <= 0)
            return BadRequest(ApiResponse<string>.Fail("Vui lòng chọn điểm lưu thông"));

        var tenantId = GetTenantId();
        var reader = await db.Readers.FirstOrDefaultAsync(x => x.Id == r.ReaderId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (reader == null) return BadRequest(ApiResponse<string>.Fail("Không tìm thấy bạn đọc"));

        var readerCheck = await CheckReaderCanBorrowAsync(reader);
        if (readerCheck != null) return BadRequest(ApiResponse<string>.Fail(readerCheck));

        var mappedStoreIds = await db.CircPlaceStores
            .Where(x => x.CircPlaceId == r.CircPlaceId && x.IsDelete != 2)
            .Select(x => x.StoreId).ToListAsync();

        var userId = GetCurrentUserId();
        var actorName = await GetActorNameAsync(userId);
        var (ok, message, bookOut) = await TryCheckoutBarcodeAsync(reader, r.Barcode ?? "", r.CircPlaceId, mappedStoreIds, userId, tenantId, actorName);
        if (!ok) return BadRequest(ApiResponse<string>.Fail(message));

        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { success = true, message, borrowId = bookOut!.Id }));
    }

    [HttpPost("Checkout/Bulk")]
    [Permission("BORROW", "add")]
    public async Task<IActionResult> CheckoutBulk([FromBody] CheckoutBulkRequest r)
    {
        if (r.CircPlaceId is null || r.CircPlaceId <= 0)
            return BadRequest(ApiResponse<string>.Fail("Vui lòng chọn điểm lưu thông"));

        var tenantId = GetTenantId();
        var reader = await db.Readers.FirstOrDefaultAsync(x => x.Id == r.ReaderId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (reader == null) return BadRequest(ApiResponse<string>.Fail("Không tìm thấy bạn đọc"));

        var readerCheck = await CheckReaderCanBorrowAsync(reader);
        if (readerCheck != null) return BadRequest(ApiResponse<string>.Fail(readerCheck));

        var barcodes = (r.Barcodes ?? new List<string>())
            .Select(b => b?.Trim()).Where(b => !string.IsNullOrEmpty(b))
            .Distinct().Take(1000).ToList();
        if (barcodes.Count == 0) return BadRequest(ApiResponse<string>.Fail("Danh sách ĐKCB trống"));

        var mappedStoreIds = await db.CircPlaceStores
            .Where(x => x.CircPlaceId == r.CircPlaceId && x.IsDelete != 2)
            .Select(x => x.StoreId).ToListAsync();

        var userId = GetCurrentUserId();
        var actorName = await GetActorNameAsync(userId);
        var items = new List<(string Barcode, bool Success, string Message, BookOut? Entity)>();
        foreach (var bc in barcodes)
        {
            var (ok, message, bookOut) = await TryCheckoutBarcodeAsync(reader, bc!, r.CircPlaceId, mappedStoreIds, userId, tenantId, actorName);
            items.Add((bc!, ok, message, bookOut));
        }

        await db.SaveChangesAsync();

        var result = items.Select(i => new { barcode = i.Barcode, success = i.Success, message = i.Message, borrowId = i.Entity?.Id }).ToList();
        return Ok(ApiResponse<object>.Ok(new
        {
            totalRequested = barcodes.Count,
            successCount   = items.Count(i => i.Success),
            failCount      = items.Count(i => !i.Success),
            items          = result
        }));
    }

    /// Rule bạn đọc — khóa/hết hạn/đang quá hạn — dùng chung cho mượn đơn lẻ và mượn nhiều, chỉ cần
    /// kiểm tra 1 lần mỗi request (không đổi giữa các ĐKCB trong cùng 1 lượt mượn nhiều).
    private async Task<string?> CheckReaderCanBorrowAsync(Reader reader)
    {
        if (reader.Status != 2)
            return "Bạn đọc không được phép mượn tài liệu (thẻ bị khoá)";
        if (reader.ExpireDate.HasValue && reader.ExpireDate < DateTime.Today)
            return "Thẻ bạn đọc đã hết hạn";

        var hasOverdue = await db.BookOuts.AnyAsync(x =>
            x.ReaderId == reader.Id && x.Status == "O" && x.DueDate.HasValue && x.DueDate < DateTime.Now && x.IsDelete != 2);
        if (hasOverdue) return "Bạn đọc đang có tài liệu quá hạn, không được phép mượn thêm";

        return null;
    }

    /// Kiểm tra + mượn 1 ĐKCB (barcode tồn tại/sẵn sàng/không trùng, kho thuộc điểm lưu thông, tính hạn trả
    /// theo PolicyCirc) — thêm BookOut vào context nhưng KHÔNG SaveChanges, để gọi hàng loạt rồi lưu 1 lần.
    private async Task<(bool Ok, string Message, BookOut? Entity)> TryCheckoutBarcodeAsync(
        Reader reader, string barcodeValue, int? circPlaceId, List<long> mappedStoreIds, long? userId, long? tenantId,
        string? actorName)
    {
        // Đợt 20: mã ĐKCB duy nhất theo đơn vị → bản sách phải thuộc đơn vị của phiên (tài khoản hệ thống: đơn vị của
        // bạn đọc), không lấy bừa bản trùng mã ở đơn vị khác.
        tenantId ??= reader.TenantId;
        var (barcode, _) = await BarcodeTenantLookup.ByValueAsync(db.Barcodes, barcodeValue, tenantId, normalize: true);
        if (barcode == null) return (false, "Mã vạch không tồn tại", null);

        if (barcode.Status != "R")
            return (false, barcode.Status == "B" ? "Tài liệu đang được mượn" : "Tài liệu không sẵn sàng cho mượn", null);

        var duplicate = await db.BookOuts.AnyAsync(x => x.Status != "R" && x.IsDelete != 2
            && (x.Reg_Seq_Id == barcode.Id
                || (x.Reg_Seq_Id == null && x.TenantId == barcode.TenantId && x.Barcode == barcode.BarcodeValue)));
        if (duplicate) return (false, "Tài liệu đang được mượn", null);

        if (mappedStoreIds.Count > 0 && (!barcode.Store.HasValue || !mappedStoreIds.Contains(barcode.Store.Value)))
            return (false, "Tài liệu không thuộc kho được phép mượn tại điểm lưu thông này", null);

        var readerTypeId = (int?)reader.ReaderTypeId;
        var policy = await db.PolicyCircs.FirstOrDefaultAsync(x =>
            x.ReaderType == readerTypeId && x.CircPlace == circPlaceId && x.IsDelete != 2)
            ?? await db.PolicyCircs.FirstOrDefaultAsync(x =>
            x.CircPlace == circPlaceId && x.ReaderType == null && x.IsDelete != 2);
        var loanDays = policy?.NumberOfDate ?? 14;

        var bookOut = new BookOut
        {
            ReaderId       = reader.Id,
            Barcode        = barcode.BarcodeValue,
            BorrowDate     = DateTime.Now,
            DueDate        = DateTime.Now.AddDays(loanDays),
            CircPlace      = circPlaceId,
            Store          = barcode.Store,
            Reg_Seq_Id     = barcode.Id,
            Status         = "O",
            UserId         = userId,
            TenantId       = tenantId,
            CreatedRowBy   = userId,
            CreatedRowDate = DateTime.Now,
            PublicId       = Guid.NewGuid()
        };
        db.BookOuts.Add(bookOut);
        barcode.Status         = "B";
        barcode.UpdateRowBy    = userId;
        barcode.UpdatedRowDate = DateTime.Now;

        // Lịch sử thay đổi chi tiết (Đợt 16) — không có ô lý do cho Checkout (đúng scope LRC, chỉ Renew/
        // NoteEdit mới bắt buộc lý do), hiển thị "Chưa ghi nhận lý do" ở trang xem. Queue trước, SaveChanges
        // do nơi gọi thực hiện (atomic với BookOut/Barcode vừa đổi — kể cả khi gọi hàng loạt ở CheckoutBulk).
        EntityAuditService.QueueEntityChangeLog(db, "LoanTransaction", bookOut.PublicId, userId ?? 0, actorName,
            tenantId, "Checkout",
            reason: null,
            changes:
            [
                new EntityAuditService.FieldChange { Field = "Status", OldValue = null, NewValue = bookOut.Status },
                new EntityAuditService.FieldChange { Field = "BorrowDate", OldValue = null, NewValue = bookOut.BorrowDate?.ToString("O") },
                new EntityAuditService.FieldChange { Field = "DueDate", OldValue = null, NewValue = bookOut.DueDate?.ToString("O") },
            ],
            ip: HttpContext.Connection.RemoteIpAddress?.ToString());

        return (true, "Mượn sách thành công", bookOut);
    }

    [HttpPost("Return")]
    [Permission("BORROW", "edit")]
    public async Task<IActionResult> Return([FromBody] LoanActionRequest r)
    {
        if (r.CircPlaceId is null || r.CircPlaceId <= 0)
            return BadRequest(ApiResponse<string>.Fail("Vui lòng chọn điểm lưu thông"));

        var tenantId = GetTenantId();
        BookOut? bookOut = null;
        if (r.BorrowId.HasValue)
            bookOut = await db.BookOuts.FirstOrDefaultAsync(x => x.Id == r.BorrowId && x.IsDelete != 2
                && (!tenantId.HasValue || x.TenantId == tenantId));
        else if (!string.IsNullOrEmpty(r.Barcode))
        {
            var normalizedBarcode = r.Barcode.Trim().ToLower();
            var open = await db.BookOuts.Where(x => x.Barcode != null && x.Barcode.Trim().ToLower() == normalizedBarcode && x.Status != "R" && x.IsDelete != 2
                && (!tenantId.HasValue || x.TenantId == tenantId)).Take(2).ToListAsync();
            // Tài khoản hệ thống quét mã đang được mượn ở 2 đơn vị khác nhau → không đoán (Đợt 20).
            if (open.Count > 1 && open[0].TenantId != open[1].TenantId)
                return BadRequest(ApiResponse<string>.Fail(BarcodeTenantLookup.AmbiguousMessage));
            bookOut = open.FirstOrDefault();
        }

        if (bookOut == null)
        {
            return BadRequest(ApiResponse<string>.Fail(
                !string.IsNullOrEmpty(r.Barcode)
                    ? "Không tìm thấy lượt mượn đang hoạt động cho mã KCB này."
                    : "Không tìm thấy phiếu mượn."));
        }

        var userId = GetCurrentUserId();
        var returnDate = DateTime.Now;

        bookOut.Status         = "R";
        bookOut.UpdateRowBy    = userId;
        bookOut.UpdatedRowDate = returnDate;

        // Lấy thông tin bạn đọc và nhan đề để trả về cho FE
        var reader = bookOut.ReaderId.HasValue
            ? await db.Readers.FirstOrDefaultAsync(x => x.Id == bookOut.ReaderId)
            : null;

        string? bibTitle = null;
        if (!string.IsNullOrEmpty(bookOut.Barcode))
        {
            // Đúng bản sách của phiếu mượn (Reg_Seq_Id) — trước đây tra theo chuỗi mã có thể đổi trạng thái bản trùng mã
            // của đơn vị khác sang "R" trong khi bản thật vẫn kẹt "đang mượn".
            var bc = await BarcodeTenantLookup.ForTransactionAsync(db, bookOut.Reg_Seq_Id, bookOut.Barcode, bookOut.TenantId);
            if (bc != null)
            {
                bc.Status         = "R";
                bc.UpdateRowBy    = userId;
                bc.UpdatedRowDate = returnDate;
            }
            if (bc?.BibId != null)
            {
                var xml = await db.BibXmls.FirstOrDefaultAsync(x => x.BibId == bc.BibId);
                bibTitle = xml?.Title;
            }
        }

        db.BookIns.Add(new BookIn
        {
            ReaderId       = bookOut.ReaderId,
            Barcode        = bookOut.Barcode,
            BorrowDate     = bookOut.BorrowDate,
            DueDate        = bookOut.DueDate,
            ReturnDate     = returnDate,
            BookOutId      = bookOut.Id,
            Renew          = bookOut.Renew,
            CircPlace      = bookOut.CircPlace,
            StoreId        = bookOut.Store,
            UserId         = (int?)userId,
            TenantId       = bookOut.TenantId ?? tenantId, // lượt trả thuộc đơn vị của lượt mượn
            CreatedRowBy   = userId,
            CreatedRowDate = returnDate,
            PublicId       = Guid.NewGuid()
        });

        // Lịch sử thay đổi chi tiết (Đợt 16) — ghi vào cùng LoanTransaction:{BookOut.PublicId} của phiếu
        // mượn gốc (không phải PublicId của BookIn vừa tạo), đúng quy ước "BookIn gắn vào lịch sử
        // BookOut.PublicId". Không có ô lý do cho Return (đúng scope LRC).
        EntityAuditService.QueueEntityChangeLog(db, "LoanTransaction", bookOut.PublicId, userId ?? 0,
            await GetActorNameAsync(userId), tenantId, "Return",
            reason: null,
            changes:
            [
                new EntityAuditService.FieldChange { Field = "Status", OldValue = "O", NewValue = bookOut.Status },
                new EntityAuditService.FieldChange { Field = "ReturnDate", OldValue = null, NewValue = returnDate.ToString("O") },
            ],
            ip: HttpContext.Connection.RemoteIpAddress?.ToString());

        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            success    = true,
            readerName = reader != null ? $"{reader.FirstName} {reader.LastName}".Trim() : null,
            bibTitle,
            cardNo     = reader?.Cardno
        }));
    }

    [HttpPost("Renew")]
    [Permission("BORROW", "edit")]
    public async Task<IActionResult> Renew([FromBody] LoanActionRequest r)
    {
        if (r.CircPlaceId is null || r.CircPlaceId <= 0)
            return BadRequest(ApiResponse<string>.Fail("Vui lòng chọn điểm lưu thông"));
        if (!r.BorrowId.HasValue) return BadRequest(ApiResponse<string>.Fail("borrowId is required"));
        if (string.IsNullOrWhiteSpace(r.Reason))
            return BadRequest(ApiResponse<string>.Fail("Vui lòng nhập lý do gia hạn"));
        var tenantId = GetTenantId();
        var bookOut = await db.BookOuts.FirstOrDefaultAsync(x => x.Id == r.BorrowId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (bookOut == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy phiếu mượn"));

        var reader = await db.Readers.FirstOrDefaultAsync(x => x.Id == bookOut.ReaderId);
        var readerTypeId = (int?)reader?.ReaderTypeId;
        var policy = await db.PolicyCircs.FirstOrDefaultAsync(x =>
            x.ReaderType == readerTypeId && x.CircPlace == bookOut.CircPlace && x.IsDelete != 2)
            ?? await db.PolicyCircs.FirstOrDefaultAsync(x => x.ReaderType == readerTypeId && x.IsDelete != 2);

        // Chính sách gia hạn: chặn nếu bạn đọc đã gia hạn đủ/vượt số lần tối đa cho phép
        var currentRenewCount = bookOut.Renew ?? 0;
        if (policy?.NumberOfRenew is > 0 && currentRenewCount >= policy.NumberOfRenew.Value)
            return BadRequest(ApiResponse<string>.Fail($"Bạn đọc đã gia hạn đủ số lần cho phép ({policy.NumberOfRenew.Value} lần)"));

        var renewDays = policy?.NumberOfRenewDays ?? 7;

        // C_RENEW_DATE: 0 = Hạn trả cũ + số ngày gia hạn, 1 = Ngày gia hạn (hôm nay) + số ngày gia hạn
        var renewDateMode = await sysParam.GetValueAsync("C_RENEW_DATE");
        var baseDate = renewDateMode == "1" ? DateTime.Now : (bookOut.DueDate ?? DateTime.Now);
        var oldDueDate = bookOut.DueDate;

        bookOut.Renew          = currentRenewCount + 1;
        bookOut.DueDate        = baseDate.AddDays(renewDays);
        var renewUserId = GetCurrentUserId();
        bookOut.UpdateRowBy    = renewUserId;
        bookOut.UpdatedRowDate = DateTime.Now;

        // Lịch sử thay đổi chi tiết (Đợt 16) — bổ sung bên cạnh WriteLoanUserLogAsync (dòng cũ, giữ
        // nguyên định dạng tự do). Queue trước SaveChangesAsync để atomic với thay đổi DueDate.
        EntityAuditService.QueueEntityChangeLog(db, "LoanTransaction", bookOut.PublicId, renewUserId ?? 0,
            await GetActorNameAsync(renewUserId), tenantId, "Renew", r.Reason,
            changes: [new EntityAuditService.FieldChange { Field = "DueDate", OldValue = oldDueDate?.ToString("O"), NewValue = bookOut.DueDate?.ToString("O") }],
            ip: HttpContext.Connection.RemoteIpAddress?.ToString());

        await db.SaveChangesAsync();
        await WriteLoanUserLogAsync("Renew", bookOut.Id,
            $"Gia hạn phiếu mượn #{bookOut.Id}: hạn {oldDueDate:dd/MM/yyyy} → {bookOut.DueDate:dd/MM/yyyy}. Lý do: {r.Reason}");
        return Ok(ApiResponse<object>.Ok(new { success = true, dueDate = bookOut.DueDate, renewCount = bookOut.Renew }));
    }


    [HttpPost("Note")]
    [Permission("BORROW", "edit")]
    public async Task<IActionResult> Note([FromBody] LoanActionRequest r)
    {
        if (!r.BorrowId.HasValue) return BadRequest(ApiResponse<string>.Fail("borrowId is required"));
        if (string.IsNullOrWhiteSpace(r.Reason))
            return BadRequest(ApiResponse<string>.Fail("Vui lòng nhập lý do sửa ghi chú"));
        var tenantId = GetTenantId();
        var bookOut = await db.BookOuts.FirstOrDefaultAsync(x => x.Id == r.BorrowId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (bookOut == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy phiếu mượn"));
        var oldNote = bookOut.Note;
        bookOut.Note          = r.Note;
        var noteUserId = GetCurrentUserId();
        bookOut.UpdateRowBy   = noteUserId;
        bookOut.UpdatedRowDate = DateTime.Now;

        EntityAuditService.QueueEntityChangeLog(db, "LoanTransaction", bookOut.PublicId, noteUserId ?? 0,
            await GetActorNameAsync(noteUserId), tenantId, "NoteEdit", r.Reason,
            changes: [new EntityAuditService.FieldChange { Field = "Note", OldValue = oldNote, NewValue = r.Note }],
            ip: HttpContext.Connection.RemoteIpAddress?.ToString());

        await db.SaveChangesAsync();
        await WriteLoanUserLogAsync("NoteEdit", bookOut.Id,
            $"Sửa ghi chú phiếu mượn #{bookOut.Id}: \"{oldNote}\" → \"{r.Note}\". Lý do: {r.Reason}");
        return Ok(ApiResponse<string>.Ok("Đã cập nhật ghi chú"));
    }

    /// <summary>Ghi UserLog cho Renew/NoteEdit (Đợt 14) — 2 thao tác duy nhất trên BookOut trước đây không
    /// có dấu vết audit. Gộp lý do vào Action text (UserLog không có cột Reason riêng), đúng quy ước đơn
    /// giản hiện có; log lỗi không chặn luồng chính, đúng cách BaseRepository.WriteUserLogAsync đang làm.</summary>
    private async Task WriteLoanUserLogAsync(string actionType, long bookOutId, string action)
    {
        try
        {
            db.UserLogs.Add(new UserLog
            {
                UserId      = GetCurrentUserId(),
                ActionType  = actionType,
                Object      = nameof(BookOut),
                Action      = action,
                Submited    = DateTime.Now,
                Ip          = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Application = "ELIBAPI",
                TenantId    = GetTenantId(),
            });
            await db.SaveChangesAsync();
        }
        catch { /* không để lỗi UserLog làm hỏng luồng chính */ }
    }

    /// Chụp tên actor tại thời điểm ghi cho lịch sử thay đổi chi tiết (Đợt 16) — không suy đoán tên khi
    /// tài khoản đổi tên sau này.
    private async Task<string?> GetActorNameAsync(long? userId)
    {
        if (userId is not long id) return null;
        return await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.FullName ?? u.LoginName).FirstOrDefaultAsync();
    }

    private long? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(claim, out var id) ? id : null;
    }

    private long? GetTenantId()
    {
        var claim = User.FindFirst("TenantId")?.Value;
        return long.TryParse(claim, out var id) ? id : null;
    }
}

public class ReaderSnapshotRequest
{
    public string? CardNo     { get; set; }
    public int?    CircPlaceId { get; set; }
}

public class CheckoutRequest
{
    public long?   ReaderId    { get; set; }
    public string? Barcode     { get; set; }
    public int?    CircPlaceId { get; set; }
}

public class CheckoutBulkRequest
{
    public long?         ReaderId    { get; set; }
    public List<string>? Barcodes    { get; set; }
    public int?          CircPlaceId { get; set; }
}
