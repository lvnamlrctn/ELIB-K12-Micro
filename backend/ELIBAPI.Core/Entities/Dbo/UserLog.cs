using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

[Table("UserLog", Schema = "dbo")]
public class UserLog
{
    [Key] public long     Id          { get; set; }
    public Guid      PublicId    { get; set; } = Guid.NewGuid();
    public long?    UserId      { get; set; }
    public string?  ActionType  { get; set; }
    public string?  Object      { get; set; }
    public string?  Action      { get; set; }
    public DateTime? Submited   { get; set; }
    public string?  Ip          { get; set; }
    public string?  Application { get; set; }
    public string?  PortalId    { get; set; }

    public long?     TenantId {get;set;}
    [NotMapped] public string? TenantName { get; set; }

}

