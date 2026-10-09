namespace ELIBAPI.Core.DTOs.Response;

public class UserPermissionResponse
{
    public long    ModuleId           { get; set; }
    public Guid    ModulePublicId     { get; set; }
    public string? ModuleName         { get; set; }
    public long?   ParentId           { get; set; }
    public string? ModuleCode         { get; set; }
    /// <summary>cms.Module.Link — frontend dùng dự phòng cho route chưa khai mã quyền (admin-perm-codes.ts).</summary>
    public string? Link               { get; set; }
    public int?    SortOrder          { get; set; }
    public Guid?   PermissionPublicId { get; set; }
    public byte?   Can_Access         { get; set; }
    public byte?   Can_View           { get; set; }
    public byte?   Can_Add            { get; set; }
    public byte?   Can_Edit           { get; set; }
    public byte?   Can_Delete         { get; set; }
    public List<UserPermissionResponse> Children { get; set; } = [];
}
