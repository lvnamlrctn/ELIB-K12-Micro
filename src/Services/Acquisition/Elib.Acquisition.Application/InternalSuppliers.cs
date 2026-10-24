using Elib.BuildingBlocks.Crud;
using Elib.Acquisition.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Acquisition.Application;

/// <summary>Nhà cung cấp cho service khác chọn (serials: đăng ký đặt báo) — chỉ mã công khai và tên.</summary>
public sealed record SupplierOption(Guid PublicId, string Name);

/// <summary>Danh sách nhà cung cấp chưa xoá của đơn vị, sắp theo tên. Đơn vị chưa dùng bổ sung thì rỗng.</summary>
public sealed class InternalSuppliers(ICrudDbContext db)
{
    public async Task<IReadOnlyList<SupplierOption>> ListAsync(CancellationToken ct) =>
        await db.Set<Supplier>().AsNoTracking().OrderBy(s => s.Name).Select(s => new SupplierOption(s.PublicId, s.Name)).ToListAsync(ct);
}
