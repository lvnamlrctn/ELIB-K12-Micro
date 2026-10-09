using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("C_Fine_Ticket", Schema = "PrintBook")]
public class CFineTicket
{
    [Key] public long Id { get; set; }
    public string?   Code           { get; set; }  // Số phiếu, vd "PT017"
    public long?     ReaderId       { get; set; }
    public DateTime? FineDate       { get; set; }
    public int?      Status         { get; set; }  // 1=Đang xử lý, 2=Đã hoàn thành
    public int?      Lanphat        { get; set; }  // lần phạt thứ mấy của bạn đọc này
    public double?   DiscountAmount { get; set; }  // Giảm trừ
    public double?   PaidAmount     { get; set; }  // Tiền nộp
    public double?   TotalAmount    { get; set; }  // Tổng cộng (snapshot, tính lại mỗi lần Save)
    public int?      OwesDocument   { get; set; }  // Nợ tài liệu (2=có)
    public long?     FineTypeId     { get; set; }  // Lý do — FK CFineType.Id
    public int?      FineMethodId   { get; set; }  // Hình thức — FK CFineMethod.Id
    public string?   Note           { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId       { get; set; }
    public Guid      PublicId       { get; set; }

    [NotMapped] public string? TenantName { get; set; }
}
