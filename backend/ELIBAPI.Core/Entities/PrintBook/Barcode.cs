using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("Barcode", Schema = "PrintBook")]
public class Barcode
{
    [Key] public long Id { get; set; }
    public string?  BarcodeNumber_str { get; set; }
    [Column("Barcode")] public string?  BarcodeValue  { get; set; }
    public int?     BarcodeNumber     { get; set; }
    public int?     Store             { get; set; }
    public long?    BibId             { get; set; }
    public long?    Receipt_Id        { get; set; }
    public long?    MapObjectId       { get; set; }  // FK-by-convention -> map.MapObject.Id (giá đang xếp)
    public long?    MapShelfRowId     { get; set; }  // FK-by-convention -> map.MapShelfRow.Id (ngăn DDC đang xếp)
    public string?   Status         { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

