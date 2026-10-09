using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

[Table("Tenant", Schema = "dbo")]
public class Tenant
{
    [Key] public long Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    // Audit Trail
    public int? IsDelete { get; set; }
    public long? CreatedRowBy { get; set; }
    public long? UpdateRowBy { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }

    public Guid PublicId { get; set; }

    public string? Host     { get; set; }
    public string? LogoText { get; set; }
    public string? LogoUrl  { get; set; }
}
