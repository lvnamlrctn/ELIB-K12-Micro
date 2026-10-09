using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("Cabinet", Schema = "PrintBook")]
public class Cabinet
{
    [Key] public long Id { get; set; }
    public string? Name        { get; set; }
    public string? Code        { get; set; }
    public long?   CircPlaceId { get; set; }
    public string? Note        { get; set; }
    public double? PositionX   { get; set; }
    public double? PositionY   { get; set; }
    public int?    Rows        { get; set; }
    public int?    Cols        { get; set; }
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

