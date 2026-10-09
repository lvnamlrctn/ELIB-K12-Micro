using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("Log_BienMuc", Schema = "PrintBook")]
public class LogBienMuc
{
    [Key] public long Id { get; set; }
    public long?     UserId   { get; set; }
    public DateTime? Submited { get; set; }
    public string?   Status   { get; set; }
    public long?     BibId    { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

