using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("SERIALITEM", Schema = "PrintBook")]
public class SerialItem
{
    [Key] public long   ID               { get; set; }
    public long?   SUBSCRIPTION_ID   { get; set; }
    public string? SERIAL_SEQ        { get; set; }
    public int?    SERIAL_SEQ_X      { get; set; }
    public int?    SERIAL_SEQ_Y      { get; set; }
    public int?    SERIAL_SEQ_Z      { get; set; }
    public int?    IS_SPECIAL        { get; set; }
    public int?    STATUS            { get; set; }
    public int?    QUANTITY          { get; set; }
    public DateTime? PLANNED_DATE    { get; set; }
    public string? NOTE              { get; set; }
    public DateTime? PUBLISHED_DATE  { get; set; }
    public DateTime? CLAIM_DATE      { get; set; }
    public int?    CLAIM_COUNT       { get; set; }
    public bool?   IsMerged          { get; set; }
    public int?    SortOrder         { get; set; }
    public int?    IsDelete          { get; set; }
    public long?   CreatedRowBy      { get; set; }
    public long?   UpdateRowBy       { get; set; }
    public DateTime? CreatedRowDate  { get; set; }
    public DateTime? UpdatedRowDate  { get; set; }
    public long?   TenantId      { get; set; }
    public Guid    PublicId          { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

