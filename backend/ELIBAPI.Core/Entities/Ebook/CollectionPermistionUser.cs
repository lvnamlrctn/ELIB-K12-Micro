using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Ebook;

[Table("CollectionPermistionUser", Schema = "Ebook")]
public class CollectionPermistionUser
{
    [Key] public long Id           { get; set; }
    public long? CollectionId { get; set; }
    public long? GroupUserId  { get; set; }
    public int?  CanAdd       { get; set; }
    public int?  CanEdit      { get; set; }
    public int?  CanDelete    { get; set; }
    public int?  CanView      { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
}

