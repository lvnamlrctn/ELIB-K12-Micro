using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("BookOut", Schema = "PrintBook")]
public class BookOut
{
    [Key] public long Id { get; set; }
    public long?     ReaderId   { get; set; }
    public DateTime? BorrowDate { get; set; }
    public DateTime? DueDate    { get; set; }
    public long?     UserId     { get; set; }
    public int?      Renew      { get; set; }
    public string?   Note       { get; set; }
    public int?      CircPlace  { get; set; }
    public string?   Barcode    { get; set; }
    public long?     RequestId  { get; set; }
    public string?   Status     { get; set; }
    public long?     Reg_Seq_Id { get; set; }
    public long?     Store      { get; set; }
    public long?     Update_By  { get; set; }
    public double?   FineValue  { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

