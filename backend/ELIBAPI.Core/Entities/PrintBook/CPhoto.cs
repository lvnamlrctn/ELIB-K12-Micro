using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("C_photo", Schema = "PrintBook")]
public class CPhoto
{
    [Key] public long Id { get; set; }
    public string?   Barcode    { get; set; }
    public long?     Reader_Id  { get; set; }
    public int?      Frompage   { get; set; }
    public int?      ToPage     { get; set; }
    public double?   Price      { get; set; }
    public DateTime? PhotoDate  { get; set; }
    public int?      Copy       { get; set; }
    public double?   TotalAmount { get; set; }  // Thành tiền = (ToPage - Frompage + 1) * Copy * Price
    public int?      IsPaid     { get; set; }   // 2 = đã thanh toán, 1/null = chưa
    public int?      Status         { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }

    [NotMapped] public string? TenantName { get; set; }
}

