using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("C_Fine", Schema = "PrintBook")]
public class CFine
{
    [Key] public long Id { get; set; }
    public long?     ReaderId      { get; set; }
    public DateTime? FineDate      { get; set; }
    public string?   Fine_type_id  { get; set; }
    public DateTime? Returndate    { get; set; }
    public string?   Note          { get; set; }
    public double?   Value         { get; set; }
    public int?      Fine_method_id { get; set; }
    public long?     Borrow_Id     { get; set; }
    public DateTime? BorrowDate    { get; set; }
    public int?      Created_by    { get; set; }
    public string?   Barcode       { get; set; }
    public int?      Lanphat       { get; set; }
    public long?     Bibid         { get; set; }
    public long?     TicketId      { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

