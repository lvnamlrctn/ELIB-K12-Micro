using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("KiemKe", Schema = "PrintBook")]
public class KiemKe
{
    [Key] public int    ID              { get; set; }
    public string?  Dkcb           { get; set; }
    public int?     StoreId        { get; set; }
    public DateTime? Submited      { get; set; }
    public int?     IsDelete       { get; set; }
    public long?    CreatedRowBy   { get; set; }
    public long?    UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId   { get; set; }
    public Guid     PublicId       { get; set; }
}

