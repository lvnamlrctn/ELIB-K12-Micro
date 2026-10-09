using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Cms;

[Table("news", Schema = "cms")]
public class News
{
    [Key] public long     Id             { get; set; }
    public string?  Title          { get; set; }
    public string?  Brief          { get; set; }
    public string?  Content        { get; set; }
    public string?  Images         { get; set; }
    public string?  Thumb          { get; set; }
    public DateTime? StartTime     { get; set; }
    public DateTime? EndTime       { get; set; }
    public DateTime? CreatedDate   { get; set; }
    public DateTime? LastUpDate    { get; set; }
    public long?    CreatedBy      { get; set; }
    public long?    UpdateBy       { get; set; }
    public long?    CategoryId     { get; set; }
    public int?     EventId        { get; set; }
    public string?  PortalId       { get; set; }
    public string?  Language       { get; set; }
    public string?  Keyword        { get; set; }
    public string?  Author         { get; set; }
    public string?  Source         { get; set; }
    public string?  Types          { get; set; }
    public string?  Clourse        { get; set; }
    public int?     Status         { get; set; }
    public int?     AllowComment   { get; set; }
    public int?     TotalView      { get; set; }
    public string?  MetaTitle      { get; set; }
    public string?  MetaKeyword    { get; set; }
    public string?  MetaDescription { get; set; }
    public string?  MaleAudio      { get; set; }
    public string?  FaleAudio      { get; set; }
    public string?  ContentAudio   { get; set; }
    public string?  BriefAudio     { get; set; }
    public string?  TitleAudio     { get; set; }
    [NotMapped] public string? CategoryName { get; set; }
    // Audit Trail
    public int?     IsDelete       { get; set; }
    public long?    CreatedRowBy   { get; set; }
    public long?    UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId   { get; set; }
    public Guid     PublicId       { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

