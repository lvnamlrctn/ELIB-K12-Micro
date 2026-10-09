using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ELIBAPI.Core.Common;

public static class AesEncryptionHelper
{
    // Key 32 bytes, IV 16 bytes — đổi trước khi deploy và chạy Encrypt lại để lấy ENC: mới
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("ELIBAPI-MinIO-SecretKey-32Bytes!");
    private static readonly byte[] IV  = Encoding.UTF8.GetBytes("ELIBAPI-IV-16By!");

    public static string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = Key; aes.IV = IV; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
        var bytes  = Encoding.UTF8.GetBytes(plainText);
        var cipher = aes.CreateEncryptor().TransformFinalBlock(bytes, 0, bytes.Length);
        return Convert.ToBase64String(cipher);
    }

    public static string Decrypt(string base64Cipher)
    {
        using var aes = Aes.Create();
        aes.Key = Key; aes.IV = IV; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
        var cipher = Convert.FromBase64String(base64Cipher);
        var plain  = aes.CreateDecryptor().TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plain);
    }

    // Giải mã Password=ENC:... trong connection string, giữ nguyên các phần khác
    public static string DecryptConnectionString(string connectionString)
    {
        return Regex.Replace(
            connectionString,
            @"(?i)(Password=)ENC:([^;]+)",
            m =>
            {
                try
                {
                    return m.Groups[1].Value + Decrypt(m.Groups[2].Value);
                }
                catch (FormatException)
                {
                    throw new InvalidOperationException(
                        $"Connection string chứa ENC: password không hợp lệ ('{m.Groups[2].Value}'). " +
                        "Dùng AesEncryptionHelper.Encrypt(\"password\") để tạo giá trị đúng.");
                }
            });
    }
}
