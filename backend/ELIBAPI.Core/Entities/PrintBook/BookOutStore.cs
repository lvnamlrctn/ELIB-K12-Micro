using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

// Sách tạm thời ra khỏi kho vì lý do khác cho mượn (đi photo, triển lãm, xuất Huyện...) — 1 dòng = 1
// vòng đời Xuất kho -> Nhập kho của 1 ĐKCB. Khác BookOut (mượn/trả bạn đọc) và AbMove (điều chuyển kho).
[Table("BookOutStore", Schema = "PrintBook")]
public class BookOutStore
{
    [Key] public long Id { get; set; }
    public long?     BarcodeId              { get; set; }
    public string?   DelivererName          { get; set; }
    public string?   ReceiverName           { get; set; }
    public int?      ReasonId               { get; set; }
    public int?      UnitId                 { get; set; }
    public int?      ExhibitionLocationId   { get; set; }
    public bool      ReturnBarcodeAtLibrary { get; set; }
    public int?      Store                  { get; set; }
    public DateTime? ExportDate             { get; set; }
    public DateTime? ImportDate             { get; set; }
    public string?   Status                 { get; set; } // "O" = Xuất kho, "R" = đã nhập kho
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}
