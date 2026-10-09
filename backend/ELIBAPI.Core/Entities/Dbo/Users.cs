using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

[Table("Users", Schema = "dbo")]
public class Users
{
    [Key] public long     Id             { get; set; }
    public string?  FullName       { get; set; }
    public string?  LoginName      { get; set; }
    public string?  Email          { get; set; }
    public string?  Phone          { get; set; }
    public long?    TenantId   { get; set; }
    public string?  PortalId       { get; set; }
    public string?  Language       { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string?  Password       { get; set; }
    public int?     RoleId         { get; set; }
    public int?     PostionId      { get; set; }
    public int?     Status         { get; set; }
    public int?     Sex            { get; set; }
    public string?  Address        { get; set; }
    public DateTime? BirthDate     { get; set; }
    public long?    CreatedBy      { get; set; }
    public long?    UpdateBy       { get; set; }
    public DateTime? CreatedDate   { get; set; }
    public string?  Photo          { get; set; }
    public DateTime? LastUpdate    { get; set; }
    public long?    RoleWinformId  { get; set; }
    // Audit Trail
    public int?     IsDelete       { get; set; }
    public long?    CreatedRowBy   { get; set; }
    public long?    UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public Guid     PublicId       { get; set; }
    /// <summary>Đổi mỗi khi quyền/role của user đổi — claim JWT "pstamp" lệch giá trị này thì filter Permission bỏ claim,
    /// tra DB (port ELIB-LRC 09-27). Cột thêm khi khởi động (Program.cs), tự sinh ở lần đăng nhập kế tiếp.</summary>
    public string?  PermissionStamp { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

