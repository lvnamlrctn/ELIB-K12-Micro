using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("C_Fine_type", Schema = "PrintBook")]
public class CFineType
{
    [Key] public long    Id              { get; set; }
    public string?  Code            { get; set; }
    public string?  Name            { get; set; }
    public string?  Status_Reg_Id   { get; set; }
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }

    [NotMapped] public string? TenantName { get; set; }
}

