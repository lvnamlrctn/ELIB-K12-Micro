using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("SystemInfo", Schema = "PrintBook")]
public class SystemInfo
{
    [Key] [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int     Id              { get; set; }
    public string? LibraryName    { get; set; }
    public string? Address        { get; set; }
    public string? Tel            { get; set; }
    public int?    IsDelete       { get; set; }
    public long?   CreatedRowBy   { get; set; }
    public long?   UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?   TenantId   { get; set; }
    public Guid    PublicId       { get; set; }
}

