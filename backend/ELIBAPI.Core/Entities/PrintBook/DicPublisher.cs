using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("DicPublisher", Schema = "PrintBook")]
public class DicPublisher
{
    [Key] public long Id { get; set; }
    public string? DisplayName { get; set; }
    public string? AccessName  { get; set; }
    public string? UnsignName  { get; set; }
    public string? PortalId    { get; set; }
    public string? Language    { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

