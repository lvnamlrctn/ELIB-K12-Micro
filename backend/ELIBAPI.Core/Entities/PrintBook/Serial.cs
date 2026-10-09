using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("Serial", Schema = "PrintBook")]
public class Serial
{
    [Key] public long Id { get; set; }
    public long?     BibId       { get; set; }
    public DateTime? StartTime   { get; set; }
    public DateTime? EndTime     { get; set; }
    public long?     FrequencyId { get; set; }
    public long?     PatternId   { get; set; }
    public long?     StoreId     { get; set; }
    public string?   Note        { get; set; }
    public int?      LastX       { get; set; }
    public int?      LastY       { get; set; }
    public int?      LastZ       { get; set; }
    // Số bắt đầu của mẫu đánh số (X/Y/Z) — kỳ dự kiến đầu tiên mang đúng các số này (port ELIB-LRC 10-03).
    public int?      StartX      { get; set; }
    public int?      StartY      { get; set; }
    public int?      StartZ      { get; set; }
    public int?      IssueLength { get; set; }
    public int?      WeekLength  { get; set; }
    public long?     SupplierId  { get; set; }
    public int?      MonthLenght { get; set; }
    public string?   Locate      { get; set; }
    public DateTime? FirstTime   { get; set; }
    public bool?      Approved      { get; set; }
    public DateTime?  ApprovedDate  { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

