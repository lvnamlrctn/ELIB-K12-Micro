using System.Globalization;
using Elib.BuildingBlocks.Domain;

namespace Elib.Circulation.Domain;

/// <summary>
/// Lý do phạt (monolith: C_Fine_type, quyền FINE_REASONS). <see cref="Amount"/>: số tiền gợi ý khi thêm dòng phạt.
/// <see cref="ItemStatus"/> = "L": dòng phạt lý do này đóng lượt mượn đang mở và báo holdings chuyển bản sách sang "Mất"
/// (monolith: Status_Reg_Id). Mã <see cref="OverdueCode"/> dùng cho dòng quá hạn tính theo ngày — không xoá được.
/// </summary>
public sealed class FineReason : TenantEntity
{
    public const string OverdueCode = "QUAHAN";
    public const string LostCode = "MATTL";

    private FineReason() { }

    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public decimal Amount { get; private set; }
    public string? ItemStatus { get; private set; }

    public static FineReason Create(string code, string name, decimal amount, string? itemStatus)
    {
        var reason = new FineReason();
        reason.Update(code, name, amount, itemStatus);
        return reason;
    }

    public void Update(string code, string name, decimal amount, string? itemStatus)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        Code = c.Length is > 0 and <= 20 ? c : throw new BusinessRuleException("CODE_INVALID", "Mã lý do phạt bắt buộc, tối đa 20 ký tự.");
        var n = (name ?? "").Trim();
        Name = n.Length is > 0 and <= 250 ? n : throw new BusinessRuleException("NAME_INVALID", "Tên lý do phạt bắt buộc, tối đa 250 ký tự.");
        Amount = FineMoney.Validate(amount, "Số tiền phạt");
        var s = string.IsNullOrWhiteSpace(itemStatus) ? null : itemStatus.Trim().ToUpperInvariant();
        ItemStatus = s is null or Loan.LostStatus ? s : throw new BusinessRuleException("ITEM_STATUS_INVALID", "Lý do phạt chỉ đổi được trạng thái bản sách sang \"Mất\" (L).");
    }
}

public static class FineMoney
{
    public const decimal Max = 1_000_000_000m;

    public static decimal Validate(decimal amount, string what) => amount is >= 0 and <= Max && decimal.Round(amount, 2) == amount
        ? amount
        : throw new BusinessRuleException("AMOUNT_INVALID", $"{what} từ 0 đến {Max.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))}.");
}

/// <summary>Trạng thái phiếu phạt — giữ số của monolith (C_Fine_Ticket.Status).</summary>
public static class FineTicketStatus
{
    public const int Open = 1;
    public const int Done = 2;
}

/// <summary>
/// Phiếu phạt (monolith: C_Fine_Ticket + C_Fine, quyền FINES). Phiếu có dòng thì tổng = tổng các dòng; phiếu thủ công (không dòng)
/// dùng <see cref="ManualAmount"/>. Còn phải nộp = tổng − giảm trừ − đã nộp. Phiếu đã hoàn thành không thêm/xoá dòng được.
/// </summary>
public sealed class FineTicket : TenantEntity
{
    public const int MaxNoteLength = 1000;

    private FineTicket() { }

    /// <summary>Số thứ tự trong đơn vị — số phiếu hiển thị "PT001".</summary>
    public long Number { get; private set; }

    public Guid ReaderPublicId { get; private set; }
    public string CardNo { get; private set; } = "";
    public DateTimeOffset FineDate { get; private set; }
    public int Status { get; private set; } = FineTicketStatus.Open;

    /// <summary>Lần phạt thứ mấy của bạn đọc (monolith: Lanphat).</summary>
    public int Round { get; private set; }

    public decimal? ManualAmount { get; private set; }
    public decimal Total { get; private set; }
    public decimal Discount { get; private set; }
    public decimal Paid { get; private set; }

    /// <summary>Còn phải nộp = tổng − giảm trừ − đã nộp (lưu thành cột để lọc/cộng trong SQL).</summary>
    public decimal Remaining { get; private set; }

    public string? Note { get; private set; }
    public List<FineLine> Lines { get; private set; } = [];

    public string Code => FormatCode(Number);

    public bool IsOpen => Status == FineTicketStatus.Open;

    public static string FormatCode(long number) => "PT" + number.ToString("D3", CultureInfo.InvariantCulture);

    public static FineTicket Open(PatronReplica reader, long number, int round, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return new FineTicket { ReaderPublicId = reader.ReaderPublicId, CardNo = reader.CardNo, Number = number, Round = round, FineDate = now };
    }

    public FineLine AddLine(FineReason reason, decimal amount, Loan? loan, string? barcode, long? mfn, int overdueDays)
    {
        ArgumentNullException.ThrowIfNull(reason);
        if (!IsOpen) throw new BusinessRuleException("FINE_TICKET_DONE", $"Phiếu {Code} đã hoàn thành, không thêm dòng phạt được.");
        var line = new FineLine
        {
            ReasonId = reason.Id, ReasonCode = reason.Code, Amount = FineMoney.Validate(amount, "Số tiền phạt"),
            LoanPublicId = loan?.PublicId, Barcode = loan?.Barcode ?? barcode, Mfn = loan?.Mfn ?? mfn, OverdueDays = Math.Max(0, overdueDays),
        };
        Lines.Add(line);
        Recalculate();
        return line;
    }

    public void RemoveLine(FineLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (!IsOpen) throw new BusinessRuleException("FINE_TICKET_DONE", $"Phiếu {Code} đã hoàn thành, không xoá dòng phạt được.");
        Lines.Remove(line);
        Recalculate();
    }

    public void ChangeLine(FineLine line, FineReason reason, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(reason);
        line.ReasonId = reason.Id;
        line.ReasonCode = reason.Code;
        line.Amount = FineMoney.Validate(amount, "Số tiền phạt");
        Recalculate();
    }

    /// <summary>Giảm trừ, đã nộp, ghi chú, ngày phạt, số tiền phiếu thủ công; trạng thái cuối cùng.</summary>
    public void Settle(DateTimeOffset? fineDate, decimal? manualAmount, decimal discount, decimal paid, string? note, int status)
    {
        if (fineDate is { } d) FineDate = d.ToUniversalTime();
        ManualAmount = manualAmount is { } m ? FineMoney.Validate(m, "Số tiền phạt") : null;
        Discount = FineMoney.Validate(discount, "Giảm trừ");
        Paid = FineMoney.Validate(paid, "Tiền đã nộp");
        var n = note?.Trim();
        Note = string.IsNullOrEmpty(n) ? null : n.Length <= MaxNoteLength ? n : throw new BusinessRuleException("NOTE_INVALID", $"Ghi chú tối đa {MaxNoteLength} ký tự.");
        Recalculate();
        if (Discount > Total) throw new BusinessRuleException("DISCOUNT_INVALID", "Giảm trừ không được lớn hơn tổng tiền phạt.");
        Status = status is FineTicketStatus.Open or FineTicketStatus.Done
            ? status
            : throw new BusinessRuleException("STATUS_INVALID", "Trạng thái phiếu phạt không hợp lệ.");
    }

    private void Recalculate()
    {
        Total = Lines.Count > 0 ? Lines.Sum(l => l.Amount) : ManualAmount ?? 0;
        Remaining = Total - Discount - Paid;
    }
}

/// <summary>Dòng phạt (monolith: C_Fine) — một tài liệu (lượt mượn/ĐKCB) hoặc khoản tự do, một lý do.</summary>
public sealed class FineLine : Entity, ITenantOwned
{
    public long TenantId { get; set; }
    public long FineTicketId { get; set; }
    public long ReasonId { get; set; }
    public string ReasonCode { get; set; } = "";
    public decimal Amount { get; set; }
    public Guid? LoanPublicId { get; set; }
    public string? Barcode { get; set; }
    public long? Mfn { get; set; }
    public int OverdueDays { get; set; }
}
