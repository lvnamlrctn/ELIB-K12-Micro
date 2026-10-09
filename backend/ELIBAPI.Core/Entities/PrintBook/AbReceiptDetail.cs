using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("ab_receipt_detail", Schema = "PrintBook")]
public class AbReceiptDetail
{
    [Key] public long Id { get; set; }
    public long?     Receipt_Id { get; set; }
    public long?     Bibid      { get; set; }
    public int?      Amount     { get; set; }
    public double?   Price      { get; set; }
    public string?   CURRENCY   { get; set; }
    public double?   Rate       { get; set; }
    public long?     Order_Id   { get; set; }
    // Id của dòng ab_order_detail nguồn khi dòng này được thêm qua "Từ đơn đặt" — cần để so khớp
    // ReceivedAmount/RemainingAmount ở CatalogueOrderController.Lines() vì Bibid không còn dùng chung
    // giữa đơn đặt (BibOrder) và đơn nhận (Bib thật) sau khi tách bảng biểu ghi nháp. Đặt tên không gạch
    // dưới để tránh lặp lại lỗi lệch tên JSON CamelCase (xem models/cataloging/order.ts).
    public long?     OrderDetailId { get; set; }
    public DateTime? Submited   { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

