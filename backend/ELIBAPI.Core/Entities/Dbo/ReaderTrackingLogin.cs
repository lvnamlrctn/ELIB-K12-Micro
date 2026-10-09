using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

[Table("ReaderTrackingLogin", Schema = "dbo")]
public class ReaderTrackingLogin
{
    [Key] public long      Id          { get; set; }
    public long?     ReaderId    { get; set; }
    public string?   Cardnumber  { get; set; }
    public DateTime? LoginTime   { get; set; }
    public DateTime? LogOutTime  { get; set; }
    public string?   Ip          { get; set; }
    public string?   SessionId   { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
}

