using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("BibXML", Schema = "PrintBook")]
public class BibXml
{
    [Key] public long   BibId           { get; set; }
    public string? Title            { get; set; }
    public string? Author           { get; set; }
    public string? Publisher        { get; set; }
    public string? PublishDate      { get; set; }
    public string? Price            { get; set; }
    public string? Page             { get; set; }
    public int?    Bib_Worksheet_Id { get; set; }
    public int?    Bib_Type_Id      { get; set; }
    public string? Isbd             { get; set; }
    public string? Accr2            { get; set; }
    public string? Keyword          { get; set; }
    public int?    UserId           { get; set; }
    public string? DDC              { get; set; }
    public int?    IsDelete         { get; set; }
    public long?   CreatedRowBy     { get; set; }
    public long?   UpdateRowBy      { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?   TenantId     { get; set; }
    public Guid    PublicId         { get; set; }
}

