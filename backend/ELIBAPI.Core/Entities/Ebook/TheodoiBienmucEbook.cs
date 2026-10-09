using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Ebook;

[Table("TheodoiBienmucEbook", Schema = "Ebook")]
public class TheodoiBienmucEbook
{
    [Key] public long      Id       { get; set; }
    public int?      UserId   { get; set; }
    public DateTime? Submited { get; set; }
    public long?     DigId    { get; set; }
    public string?   Status   { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
}

