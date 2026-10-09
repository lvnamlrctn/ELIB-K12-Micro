using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("C_renew", Schema = "PrintBook")]
public class CRenew
{
    [Key] public long Id { get; set; }
    public long?     Borrow_id          { get; set; }
    public long?     Reg_Seq_Id         { get; set; }
    public string?   Reg_Id             { get; set; }
    public int?      Circ_Place_Id      { get; set; }
    public int?      Status_Id          { get; set; }
    public DateTime? Renew_Date         { get; set; }
    public DateTime? Duedate_Request    { get; set; }
    public int?      Active             { get; set; }
    public int?      Status_Email       { get; set; }
    public int?      Renew_Date_Num     { get; set; }
    public DateTime? Borrow_Date        { get; set; }
    public DateTime? Duedate            { get; set; }
    public long?     Reader_Id          { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

