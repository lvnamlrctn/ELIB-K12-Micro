using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Cms;

[Table("PhotoAlbum", Schema = "cms")]
public class PhotoAlbum
{
    [Key] public long    Id          { get; set; }
    public string? PortalId    { get; set; }
    public string? Name        { get; set; }
    public string? Description { get; set; }
    public string? Image       { get; set; }
    public string? Code        { get; set; }
    public string? Language    { get; set; }
    public int?    Status      { get; set; }
    public int?    SortOrder   { get; set; }
    public string? Types       { get; set; }
    public string? Postions    { get; set; }
    public int?    IsSpecial   { get; set; }
    // Audit Trail
    public int?    IsDelete       { get; set; }
    public long?   CreatedRowBy   { get; set; }
    public long?   UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?   TenantId   { get; set; }
    public Guid    PublicId       { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

