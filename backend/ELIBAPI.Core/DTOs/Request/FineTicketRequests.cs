namespace ELIBAPI.Core.DTOs.Request;

// Yêu cầu của màn hình Phiếu phạt (chuyển từ CFineTicketController sang Core để IFineTicketService dùng trực tiếp —
// tên thuộc tính giữ nguyên nên JSON frontend gửi lên không đổi).

public class BuildFineTicketRequest
{
    public long? ReaderId    { get; set; }
    public int?  CircPlaceId { get; set; }
    /// <summary>Phiếu mượn cán bộ tích chọn (vd mất tài liệu) — đưa vào phiếu phạt dù chưa quá hạn.</summary>
    public List<long>? LoanIds { get; set; }
}

public class SaveFineTicketRequest
{
    public DateTime? FineDate       { get; set; }
    public int?      Status         { get; set; }
    public double?   DiscountAmount { get; set; }
    public double?   PaidAmount     { get; set; }
    public double?   TotalAmount    { get; set; }
    public int?      OwesDocument   { get; set; }
    public long?     FineTypeId     { get; set; }
    public int?      FineMethodId   { get; set; }
    public string?   Note           { get; set; }
    public int?      CircPlaceId    { get; set; }
    public List<FineTicketLineUpdate>? Lines { get; set; }
    /// <summary>Dòng phạt cần xoá (xoá mềm) — chỉ khi phiếu chưa hoàn thành.</summary>
    public List<long>? DeletedLineIds { get; set; }
}

public class FineTicketLineUpdate
{
    /// <summary>&lt;= 0 = dòng phạt mới thêm tay (theo ĐKCB hoặc tự do).</summary>
    public long    Id         { get; set; }
    /// <summary>Chỉ dùng cho dòng mới: ĐKCB (không bắt buộc).</summary>
    public string? Barcode    { get; set; }
    public string? FineTypeId { get; set; }
    public double? Value      { get; set; }
}

public class CreateFineTicketRequest
{
    public long?     ReaderId       { get; set; }
    public DateTime? FineDate       { get; set; }
    public int?      Status         { get; set; }
    public long?     FineTypeId     { get; set; }
    public int?      FineMethodId   { get; set; }
    public double?   TotalAmount    { get; set; }
    public double?   DiscountAmount { get; set; }
    public double?   PaidAmount     { get; set; }
    public int?      OwesDocument   { get; set; }
    public string?   Note           { get; set; }
}
