namespace Elib.BuildingBlocks.Domain;

/// <summary>Bảng thuộc về một đơn vị. Query filter "Tenant" + RLS tự áp dụng; TenantId do interceptor gán, không sửa được.</summary>
public interface ITenantOwned
{
    long TenantId { get; set; }
}

/// <summary>Xoá mềm (thay cột <c>int? IsDelete</c> của monolith). Query filter "SoftDelete" tự ẩn bản ghi đã xoá.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
    long? DeletedBy { get; set; }
}

/// <summary>Cột audit (thay CreatedRowBy/CreatedRowDate/UpdateRowBy/UpdatedRowDate của monolith). Thời gian luôn UTC.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    long? CreatedBy { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
    long? UpdatedBy { get; set; }
}

/// <summary>Định danh công khai dùng trên URL/API và tham chiếu chéo service (giữ quy ước PublicId của monolith). Tự sinh Guid v7.</summary>
public interface IHasPublicId
{
    Guid PublicId { get; set; }
}

/// <summary>
/// Trạng thái kiểu monolith (cột <c>int? Status</c>): <see cref="Active"/> = 2 "Hoạt động", <see cref="Inactive"/> = 1 "Không hoạt động".
/// Danh mục CRUD có interface này mới có endpoint ChangeStatus.
/// </summary>
public interface IHasStatus
{
    const int Inactive = 1;
    const int Active = 2;

    int Status { get; }

    void ChangeStatus(int status);
}

public static class StatusRules
{
    public static int Validate(int status) => status is IHasStatus.Active or IHasStatus.Inactive
        ? status
        : throw new BusinessRuleException("STATUS_INVALID", "Trạng thái chỉ nhận 1 (không hoạt động) hoặc 2 (hoạt động).");
}

public abstract class Entity
{
    public long Id { get; set; }
}

/// <summary>Entity danh mục/nghiệp vụ chuẩn không thuộc đơn vị (dữ liệu dùng chung, bảng *_shared).</summary>
public abstract class AuditableEntity : Entity, IAuditable, ISoftDeletable, IHasPublicId
{
    public Guid PublicId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public long? DeletedBy { get; set; }
}

/// <summary>Entity chuẩn thuộc một đơn vị — lựa chọn mặc định cho hầu hết bảng nghiệp vụ.</summary>
public abstract class TenantEntity : AuditableEntity, ITenantOwned
{
    public long TenantId { get; set; }
}
