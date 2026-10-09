using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("SerialBinding", Schema = "PrintBook")]
public class SerialBinding
{
    [Key] public long      Id                { get; set; }
    public string?         AccessionNo       { get; set; }
    public long?           StoreId           { get; set; }
    public string?         VolumeTitle       { get; set; }
    public long?           SubscriptionId    { get; set; }
    public string?         SubscriptionTitle { get; set; }
    public DateTime?       BindingDate       { get; set; }
    public string?         Note              { get; set; }
    public int?            IsDelete          { get; set; }
    public Guid            PublicId          { get; set; }
    public long?           TenantId          { get; set; }
    public long?           CreatedRowBy      { get; set; }
    public long?           UpdateRowBy       { get; set; }
    public DateTime?       CreatedRowDate    { get; set; }
    public DateTime?       UpdatedRowDate    { get; set; }
}
