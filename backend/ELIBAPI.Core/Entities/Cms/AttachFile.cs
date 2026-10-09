using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Cms;

[Table("AttachFile", Schema = "cms")]
public class AttachFile
{
    [Key] public long     Id          { get; set; }
    public string?  Name        { get; set; }
    public string?  Url         { get; set; }
    public double?  FileSize    { get; set; }
    public long?    NewsId      { get; set; }
    public DateTime? CreatedDate { get; set; }
    // Audit Trail
    public int?    IsDelete       { get; set; }
    public long?   CreatedRowBy   { get; set; }
    public long?   UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?   TenantId   { get; set; }
    public Guid    PublicId       { get; set; }
}

