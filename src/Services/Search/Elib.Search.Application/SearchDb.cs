using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Elib.Search.Application;

/// <summary>DbContext của search (chỉ mục tra cứu) — tầng Application không phụ thuộc Infrastructure.</summary>
public interface ISearchDb
{
#pragma warning disable CA1716 // trùng chữ ký DbContext.Set<T>() để DbContext cài đặt sẵn
    DbSet<TEntity> Set<TEntity>() where TEntity : class;
#pragma warning restore CA1716

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    ChangeTracker ChangeTracker { get; }
}
