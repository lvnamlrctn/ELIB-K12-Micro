using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("InventoryBarcode", Schema = "PrintBook")]
public class InventoryBarcode
{
    [Key] public long Id { get; set; }
    public string? Barcode           { get; set; }
    public long?   StoreId           { get; set; }
    public long?   InventoryId       { get; set; }
    public int?    CheckStoreStatus  { get; set; }
    public int?    CheckStatus       { get; set; }
    public int?    CheckBorrow       { get; set; }
    public int?    CheckRegisteter   { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

