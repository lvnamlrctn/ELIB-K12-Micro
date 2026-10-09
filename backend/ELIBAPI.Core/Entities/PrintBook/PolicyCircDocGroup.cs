using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("PolicyCircDocGroup", Schema = "PrintBook")]
public class PolicyCircDocGroup
{
    [Key] public int Id { get; set; }
    public int  PolicyCircId       { get; set; }
    // long: trỏ BibType.Id (đổi từ DocGroup.Id kiểu int — DocGroup là bảng thừa, không dùng ở đâu khác
    // trong hệ thống; BibType là danh mục loại tài liệu chính thức, đã dùng sẵn ở bib-list/bib-type).
    public long DocGroupId         { get; set; }
    public int? NumberOfBook       { get; set; }
    public int? NumberOfRequest    { get; set; }
    public int? NumberOfRenewQty   { get; set; }
    public int? NumberOfBookAccept { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId       { get; set; }
    public Guid      PublicId       { get; set; }
}
