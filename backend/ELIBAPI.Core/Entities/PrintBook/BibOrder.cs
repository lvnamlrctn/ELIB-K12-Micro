using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("BibOrder", Schema = "PrintBook")]
public class BibOrder
{
    [Key] public long    Bibid            { get; set; }
    public long?    Mfn              { get; set; }
    public DateTime? CreatedTime     { get; set; }
    public DateTime? UpdateTime      { get; set; }
    public long?    CreatedBy        { get; set; }
    public long?    UpdateBy         { get; set; }
    public string?  Status           { get; set; }
    public long?    Bib_Worksheet_Id { get; set; }
    public long?    Bib_Type_Id      { get; set; }
    public string?  MARC_STATUS      { get; set; }
    public string?  Url              { get; set; }
    public string?  Images           { get; set; }
    public int?     IsDelete         { get; set; }
    public long?    CreatedRowBy     { get; set; }
    public long?    UpdateRowBy      { get; set; }
    public DateTime? CreatedRowDate  { get; set; }
    public DateTime? UpdatedRowDate  { get; set; }
    public long?    TenantId     { get; set; }
    public Guid     PublicId         { get; set; }
}

