using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("CircPlaceReaderType", Schema = "PrintBook")]
public class CircPlaceReaderType
{
    [Key] public int Id { get; set; }
    public int    CircPlaceId  { get; set; }
    public long   ReaderTypeId { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId       { get; set; }
    public Guid      PublicId       { get; set; }
}
