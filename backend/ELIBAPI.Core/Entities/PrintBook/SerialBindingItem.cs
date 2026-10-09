using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("SerialBindingItem", Schema = "PrintBook")]
public class SerialBindingItem
{
    [Key] public long    Id            { get; set; }
    public long?         BindingId     { get; set; }
    public long?         IssueId       { get; set; }
    public string?       SerialSeq     { get; set; }
    public DateTime?     PublishedDate { get; set; }
    public int?          IsDelete      { get; set; }
    public Guid          PublicId      { get; set; }
    public long?         CreatedRowBy  { get; set; }
    public long?         UpdateRowBy   { get; set; }
    public DateTime?     CreatedRowDate  { get; set; }
    public DateTime?     UpdatedRowDate  { get; set; }
}
