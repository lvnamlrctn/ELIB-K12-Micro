using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("PolicyCirc", Schema = "PrintBook")]
public class PolicyCirc
{
    [Key] public int Id { get; set; }
    public int? ReaderType      { get; set; }
    public int? CircPlace       { get; set; }
    public int? NumberOfDate    { get; set; }
    public int? NumberOfRenew   { get; set; }
    public int? NumberOfBook    { get; set; }
    public int? NumberOfRequest { get; set; }
    public int? Muontrung       { get; set; }
    public int? Store           { get; set; }
    public int? NumberOfBookAccept   { get; set; }
    public int? NumberOfRenewQty     { get; set; }
    public int? NumberOfRenewDays    { get; set; }
    public int? NumberOfRequestCount { get; set; }
    public int? NumberOfRequestDays  { get; set; }
    public int? AllowOpacRequest     { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }

    [NotMapped] public string? TenantName { get; set; }
}

