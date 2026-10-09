using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Cms;

[Table("Menu", Schema = "cms")]
public class Menu
{
    [Key] public long    Id        { get; set; }
    public string? Name      { get; set; }
    public long?   MenuType  { get; set; }
    public string? Link      { get; set; }
    public string? FriendUrl { get; set; }
    public int?    SortOrder { get; set; }
    public int?    Status    { get; set; }
    public string? OpenType  { get; set; }
    public string? PortalId  { get; set; }
    public string? Language  { get; set; }
    public long?   ParentId  { get; set; }
    public string? LinkType  { get; set; }
    public string? SubId     { get; set; }
    public int?    IsLogIn   { get; set; }
    public string? Icon      { get; set; }
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

