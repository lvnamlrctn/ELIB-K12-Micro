using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("BookIn", Schema = "PrintBook")]
public class BookIn
{
    [Key] public long Id { get; set; }
    public long?     ReaderId   { get; set; }
    public string?   Barcode    { get; set; }
    public DateTime? BorrowDate { get; set; }
    public DateTime? DueDate    { get; set; }
    public DateTime? ReturnDate { get; set; }
    public int?      UserId     { get; set; }
    public string?   Note       { get; set; }
    public long?     BookOutId  { get; set; }
    public int?      Renew      { get; set; }
    public long?     CircPlace  { get; set; }
    public long?     StoreId    { get; set; }
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

