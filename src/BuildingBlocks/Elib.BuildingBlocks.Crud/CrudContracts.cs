using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Elib.BuildingBlocks.Crud;

/// <summary>
/// Điều kiện tìm kiếm chuẩn — giữ tên trường của <c>SearchRequest</c> monolith để frontend port lại service Angular cũ.
/// Bỏ PortalId/Language/TenantId: đơn vị lấy từ token, không nhận từ client.
/// </summary>
public class CrudSearch
{
    public string? Keyword { get; set; }
    public int? Status { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Từ khoá đã trim + chữ thường, null nếu rỗng — so với <c>x.Name.ToLower().Contains(term)</c>.</summary>
    public string? Term => string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim().ToLowerInvariant();
}

/// <summary>Trang kết quả — tên trường như <c>PagedResult</c> monolith.</summary>
public sealed record CrudPage<T>(IReadOnlyList<T> Items, int TotalCount, int PageIndex, int PageSize)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public sealed record ChangeStatusRequest(Guid PublicId, int Status);

public enum CrudChange
{
    Added,
    Updated,
    Deleted,
    StatusChanged,

    /// <summary>Nhập hàng loạt từ Excel — một dòng nhật ký cho cả lần nhập.</summary>
    Imported,
}

/// <summary>DbContext của service — <see cref="DbContext"/> đã có sẵn hai method này, chỉ cần khai báo interface.</summary>
public interface ICrudDbContext
{
#pragma warning disable CA1716 // trùng chữ ký DbContext.Set<T>() để DbContext cài đặt sẵn; chỉ dùng từ C#
    DbSet<TEntity> Set<TEntity>() where TEntity : class;
#pragma warning restore CA1716

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Transaction + execution strategy (import Excel ghi nhiều dòng trong một transaction).</summary>
    DatabaseFacade Database { get; }
}
