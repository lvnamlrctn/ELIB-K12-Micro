using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("CircPlace", Schema = "PrintBook")]
public class CircPlace
{
    [Key] public int Id { get; set; }
    public string? Name                      { get; set; }
    public string? Work_Session               { get; set; }
    public int?    Cir_Type                  { get; set; }
    public int?    Cir_Work_Follow           { get; set; }
    public int?    Access_Request_Validtime  { get; set; }
    public int?    Limit_Book                { get; set; }
    public int?    Vitual                    { get; set; }
    public string? Code                      { get; set; }
    public int?    AutoAccept                { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

