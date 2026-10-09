using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Phát hiện lệch khoá mã hoá (<see cref="AdminTaskCrypto.Fingerprint"/>) giữa các instance
/// worker cùng chạy trong 1 môi trường — KHÔNG phải cơ chế bầu leader. Nếu bất kỳ worker nào khác (nhịp
/// tim còn trong 2 phút gần nhất) đang dùng khoá khác, worker này từ chối xử lý để tránh 2 khoá khác nhau
/// làm hỏng Payload đã mã hoá của nhau (ví dụ giữa lúc đổi khoá triển khai). ELIB v1 chỉ chạy 1 instance
/// worker nên nhánh này hiếm khi kích hoạt, nhưng giữ nguyên để an toàn khi mở rộng nhiều instance.</summary>
public class AdminTaskHealth(ELIBAPIDbContext db, AdminTaskCrypto crypto)
{
    public async Task<bool> CanProcess(string workerId, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-2);
        var others = await db.AdminWorkerHeartbeats.AsNoTracking()
            .Where(x => x.Id != workerId && x.LastSeen > cutoff).ToListAsync(ct);
        return others.All(x => x.KeyFingerprint == crypto.Fingerprint);
    }

    public async Task Beat(string workerId, CancellationToken ct)
    {
        var existing = await db.AdminWorkerHeartbeats.FirstOrDefaultAsync(x => x.Id == workerId, ct);
        if (existing == null)
            db.AdminWorkerHeartbeats.Add(new AdminWorkerHeartbeat
            {
                Id = workerId,
                LastSeen = DateTime.UtcNow,
                KeyFingerprint = crypto.Fingerprint,
            });
        else
        {
            existing.LastSeen = DateTime.UtcNow;
            existing.KeyFingerprint = crypto.Fingerprint;
        }
        await db.SaveChangesAsync(ct);
    }
}
