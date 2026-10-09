using System.Collections.Concurrent;
using System.Security.Cryptography;
using ELIBAPI.Core.Interfaces;

namespace ELIBAPI.Infrastructure.Services;

/// Implementation dùng chung cho đăng nhập bạn đọc lẫn admin (OtpTtl 5 phút, tối đa 5 lần sai).
/// Đợt 7 — Giai đoạn 4: hardening — RNG mật mã (không dùng Random.Shared), hạn cố định tính từ lúc sinh mã
/// (không gia hạn theo mỗi lần sai), và cổng lấy-1-lần-duy-nhất nguyên tử (ConcurrentDictionary.TryRemove
/// thay vì IMemoryCache TryGetValue→so sánh→Remove không nguyên tử, tránh 2 request Verify đồng thời cùng
/// mã đúng cùng trả Success=true).
public class OtpService : IOtpService
{
    private static readonly TimeSpan OtpTtl = TimeSpan.FromMinutes(5);
    private const int MaxOtpAttempts = 5;

    private static readonly ConcurrentDictionary<string, PendingOtp> _pending = new();

    public (string Otp, string SessionToken) GenerateAndCache(long subjectId, string purpose)
    {
        PruneExpired();
        var otp = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var sessionToken = Guid.NewGuid().ToString("N");
        var pending = new PendingOtp { SubjectId = subjectId, Otp = otp, ExpiresAtUtc = DateTime.UtcNow.Add(OtpTtl) };
        _pending[CacheKey(purpose, sessionToken)] = pending;
        return (otp, sessionToken);
    }

    // Không dùng IMemoryCache nữa nên không còn auto-evict theo TTL — dọn các phiên bị bỏ dở (không bao
    // giờ gọi Verify) mỗi lần sinh mã mới, tránh ConcurrentDictionary phình dần theo thời gian.
    private static void PruneExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _pending)
            if (kvp.Value.ExpiresAtUtc < now)
                _pending.TryRemove(kvp.Key, out _);
    }

    public OtpVerifyResult Verify(string sessionToken, string code, string purpose)
    {
        var key = CacheKey(purpose, sessionToken);

        // Lấy-1-lần-duy-nhất nguyên tử: remove trước rồi mới so sánh, để 2 request Verify đồng thời cùng
        // mã đúng không thể cả hai cùng đọc được entry trước khi request nào remove.
        if (!_pending.TryRemove(key, out var pending))
            return new OtpVerifyResult(false, null, "Mã đã hết hạn, vui lòng đăng nhập lại");

        if (DateTime.UtcNow > pending.ExpiresAtUtc)
            return new OtpVerifyResult(false, null, "Mã đã hết hạn, vui lòng đăng nhập lại");

        if (!string.Equals(pending.Otp, code?.Trim(), StringComparison.Ordinal))
        {
            pending.Attempts++;
            if (pending.Attempts >= MaxOtpAttempts)
                return new OtpVerifyResult(false, null, "Sai quá số lần cho phép, vui lòng đăng nhập lại");

            // Sai nhưng chưa hết lượt — thêm lại để còn cơ hội nhập lại, GIỮ NGUYÊN ExpiresAtUtc gốc
            // (không gia hạn thêm thời gian sống của mã).
            _pending[key] = pending;
            return new OtpVerifyResult(false, null, "Mã xác thực không đúng");
        }

        return new OtpVerifyResult(true, pending.SubjectId, null);
    }

    private static string CacheKey(string purpose, string sessionToken) => $"Otp:{purpose}:{sessionToken}";

    private class PendingOtp
    {
        public long SubjectId { get; set; }
        public string Otp { get; set; } = "";
        public int Attempts { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }
}
