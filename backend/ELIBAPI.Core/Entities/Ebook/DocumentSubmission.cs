using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Ebook;

/// <summary>Tài liệu nội sinh do bạn đọc nộp qua OPAC, chờ thủ thư kiểm duyệt.</summary>
[Table("DocumentSubmission", Schema = "Ebook")]
public class DocumentSubmission
{
    [Key] public long Id { get; set; }
    public long     ReaderId    { get; set; }
    public string?  Title       { get; set; }
    public string?  Author      { get; set; }
    public string?  DocType     { get; set; }
    public string?  Abstract    { get; set; }
    public string?  FileUrl     { get; set; }
    public string?  FileName    { get; set; }
    public string?  Status      { get; set; }
    public DateTime CreatedDate { get; set; }
    public int?     IsDelete    { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId    { get; set; }
}
