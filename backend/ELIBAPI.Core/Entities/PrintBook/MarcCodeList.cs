using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("marc_code_list", Schema = "PrintBook")]
public class MarcCodeList
{
    [Key] public int    ID              { get; set; }
    public string? Loai           { get; set; }
    public string? Ma             { get; set; }
    public string? Desvn          { get; set; }
    public string? Desta          { get; set; }
    public int?    IsDelete       { get; set; }
    public long?   CreatedRowBy   { get; set; }
    public long?   UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?   TenantId   { get; set; }
    public Guid    PublicId       { get; set; }
}

