using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Evaluate;

[Table("Course", Schema = "Evaluate")]
public class EvaluateCourse
{
    [Key] public long    Id             { get; set; }
    public string? Code           { get; set; }
    public string? Name           { get; set; }
    public int?    Credit         { get; set; }
    public int?    Status         { get; set; }
    public long?   DegreeId       { get; set; }
    public long?   OptionCourseId { get; set; }
    public string? KnowledgeId    { get; set; }
    public string? FileUrl        { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
}

