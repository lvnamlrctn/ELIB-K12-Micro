namespace ELIBAPI.Core.Interfaces;

/// <summary>
/// Quản lý phiên bản cache (cache versioning) theo tên phân vùng (scope) — Đợt 22, port từ ELIB-LRC.
/// Khi admin thêm/sửa/xóa 1 thực thể công khai (Banner/Link/SystemParameter/EbookCollection…), gọi
/// <see cref="Invalidate"/> để tăng version của scope đó; mọi khóa cache cũ mang version cũ tự mất hiệu lực
/// mà không cần liệt kê/xóa từng khóa. Dùng cùng <see cref="ELIBAPI.Infrastructure.Repositories.PublicBaseRepository{TEntity,TSearch}"/>.
/// </summary>
public interface ICacheInvalidator
{
    /// <summary>Phiên bản hiện tại của 1 phân vùng (vd "PublicBannerResponse") — ghép vào khóa cache.</summary>
    string GetVersion(string scope);

    /// <summary>Tăng phiên bản của phân vùng để toàn bộ cache cũ liên quan hết hiệu lực ngay.</summary>
    void Invalidate(string scope);
}
