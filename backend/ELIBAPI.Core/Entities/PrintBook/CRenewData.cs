using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("C_renew_data", Schema = "PrintBook")]
public class CRenewData
{
    [Key] public long Id { get; set; }
    public long?     Borrow_id    { get; set; }
    public long?     Reg_Seq_Id   { get; set; }
    public string?   Reg_Id       { get; set; }
    public DateTime? Renew_Date   { get; set; }
    public DateTime? Due_Date_Old { get; set; }
    public DateTime? Due_Date_New { get; set; }
    public DateTime? Borrow_Date  { get; set; }
    public long?     Reader_Id    { get; set; }
    public int?      Created_By   { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

