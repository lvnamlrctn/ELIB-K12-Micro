using System.Collections.Concurrent;
using ELIBAPI.Core.Interfaces;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Đợt 22 — port từ ELIB-LRC, không đổi gì (thuần in-memory, không cần biết tenant: khóa cache của
/// <see cref="Repositories.PublicBaseRepository{TEntity,TSearch}"/> đã tự gồm tenant qua request body/query
/// nên tăng version của 1 scope là đủ vô hiệu hoá đúng phạm vi). Đăng ký Singleton.</summary>
public class CacheInvalidator : ICacheInvalidator
{
    private readonly ConcurrentDictionary<string, long> _versions = new(StringComparer.OrdinalIgnoreCase);

    public string GetVersion(string scope)
    {
        var v = _versions.GetOrAdd(scope, 1);
        return v.ToString();
    }

    public void Invalidate(string scope)
    {
        _versions.AddOrUpdate(scope, 1, (_, current) => current + 1);
    }
}
