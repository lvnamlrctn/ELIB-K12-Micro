using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Mã hoá AES-GCM cho <c>AdminTask.Payload</c>/<c>AdminTaskChunk.Payload</c> (Đợt 10 — port từ
/// ELIB-LRC). Khoá dẫn xuất 1 lần từ <c>AdminTasks:Key</c> (fallback <c>Jwt:Key</c> — không cần thêm
/// secret mới, ELIB đã có sẵn ≥32 ký tự), dùng chung toàn hệ thống (không per-tenant — xem "Quyết định
/// thiết kế" trong kế hoạch Đợt 10). Định dạng: Base64(nonce[12] ‖ tag[16] ‖ ciphertext).</summary>
public sealed class AdminTaskCrypto
{
    private readonly Lazy<byte[]> _key;

    /// <summary>SHA-256 hex của khoá — dùng để phát hiện lệch khoá giữa các instance worker
    /// (<see cref="AdminTaskHealth"/>), không bao giờ trả qua API.</summary>
    public string Fingerprint => Convert.ToHexString(SHA256.HashData(_key.Value));

    public AdminTaskCrypto(IConfiguration config)
    {
        _key = new Lazy<byte[]>(() =>
        {
            var secret = config["AdminTasks:Key"] ?? config["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
                throw new InvalidOperationException("AdminTasks:Key (hoặc Jwt:Key) cần ít nhất 32 ký tự.");
            return SHA256.HashData(Encoding.UTF8.GetBytes("admin-task-payload-v1:" + secret));
        });
    }

    public string Protect(string text)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(text);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_key.Value, 16);
        aes.Encrypt(nonce, plain, cipher, tag);
        var output = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, output, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, output, nonce.Length + tag.Length, cipher.Length);
        return Convert.ToBase64String(output);
    }

    public string Unprotect(string text)
    {
        var data = Convert.FromBase64String(text);
        var plain = new byte[data.Length - 28];
        using var aes = new AesGcm(_key.Value, 16);
        aes.Decrypt(data.AsSpan(0, 12), data.AsSpan(28), data.AsSpan(12, 16), plain);
        return Encoding.UTF8.GetString(plain);
    }
}
