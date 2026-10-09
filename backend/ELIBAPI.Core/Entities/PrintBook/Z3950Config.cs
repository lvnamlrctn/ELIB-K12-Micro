using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("Z3950Config", Schema = "PrintBook")]
public class Z3950Config
{
    [Key] public long Id { get; set; }
    public string? Name { get; set; }
    public string? Host { get; set; }
    public string? Port { get; set; }
    public string? DatabaseName { get; set; }
    public string? Systax { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    public long? GroupId { get; set; }
    public string? Url { get; set; }

    // Audit Trail
    public int? IsDelete { get; set; }
    public long? CreatedRowBy { get; set; }
    public long? UpdateRowBy { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long? TenantId { get; set; }
    public Guid PublicId { get; set; }

    [NotMapped] public string? TenantName { get; set; }
}

