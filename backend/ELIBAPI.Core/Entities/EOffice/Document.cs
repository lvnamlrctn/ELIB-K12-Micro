using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.EOffice;

[Table("Document", Schema = "EOffice")]
public class Document
{
    [Key] public long      Id             { get; set; }
    public string?   Name           { get; set; }
    public string?   Brief          { get; set; }
    public long?     DocumentTypeId { get; set; }
    public long?     AgencyId       { get; set; }
    public DateTime? IssueDate      { get; set; }
    public DateTime? ExpireDate     { get; set; }
    public DateTime? CreatedDate    { get; set; }
    public int?      TypeId         { get; set; }
    public int?      Status         { get; set; }
    public string?   Sign           { get; set; }
    public long?     TopicId        { get; set; }
    public string?   GovDocNumber   { get; set; }
    public string?   PortalId       { get; set; }
    public string?   Language       { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
}

