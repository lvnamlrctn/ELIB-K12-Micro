using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("aacr2_subfield", Schema = "PrintBook")]
public class Aacr2Subfield
{
    [Key] public int Id { get; set; }
    public int?     Config_Id      { get; set; }
    public string?  Field          { get; set; }
    public string?  Subfield       { get; set; }
    public string?  Starttp        { get; set; }
    public string?  Stoptp         { get; set; }
    public string?  Nexttp         { get; set; }
    public int?     Subfieldindex  { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

