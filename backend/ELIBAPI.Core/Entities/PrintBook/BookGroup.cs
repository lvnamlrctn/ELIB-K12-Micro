using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("Book_Group", Schema = "PrintBook")]
public class BookGroup
{
    [Key] public long Id { get; set; }
    public long? Bib_Id1  { get; set; }
    public long? Bib_Id2  { get; set; }
    public int?  Link     { get; set; }
    public int?  Mode     { get; set; }
    public long? Fieldid  { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

