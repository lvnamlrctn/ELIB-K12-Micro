using System.Security.Cryptography;
using System.Text;

namespace ELIBAPI.Core.Common;

/// <summary>
/// Băm/so khớp mật khẩu bằng BCrypt — dùng chung cho Users và Reader. Mật khẩu mới luôn ra BCrypt
/// (tiền tố "$2"); <see cref="Verify"/> vẫn nhận diện được hash MD5 hex cũ (100% dữ liệu hiện có, vì
/// ELIB trước đây dùng MD5.HashData thuần — một chiều, không thể giải ngược để di trú hàng loạt) để
/// không khoá tài khoản nào — các service gọi nơi đăng nhập tự chịu trách nhiệm nâng cấp ngầm sau khi
/// xác thực thành công (set lại Password = Hash(plainText)).
/// </summary>
public static class PasswordHasher
{
    public static string Hash(string plainText) => BCrypt.Net.BCrypt.HashPassword(plainText);

    public static bool IsBCryptHash(string? hash) => hash != null && hash.StartsWith("$2");

    /// <summary>Chuỗi băm cũ trước khi chuyển sang BCrypt — chỉ dùng để so khớp mật khẩu chưa được
    /// nâng cấp, không dùng để tạo mật khẩu mới.</summary>
    private static string LegacyMd5(string plainText)
        => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(plainText))).ToLower();

    public static bool Verify(string plainText, string? storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;
        return IsBCryptHash(storedHash)
            ? BCrypt.Net.BCrypt.Verify(plainText, storedHash)
            : storedHash.Trim().ToLower() == LegacyMd5(plainText);
    }
}
