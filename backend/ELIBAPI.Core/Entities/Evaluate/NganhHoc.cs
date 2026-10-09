using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Evaluate;

[Table("NganhHoc", Schema = "Evaluate")]
public class NganhHoc
{
    [Key] public long    Id            { get; set; }
    public string? MajorsName    { get; set; }
    public long?   ParentId      { get; set; }
    public long?   ProgramId     { get; set; }
    public long?   AmountStudent { get; set; }
    public int?    SortOrder     { get; set; }
    public string? PortalId      { get; set; }
    public string? Language      { get; set; }
    public string? MajorsCode    { get; set; }
    public int?    Status        { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

