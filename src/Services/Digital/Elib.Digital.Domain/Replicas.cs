using Elib.BuildingBlocks.Domain;

namespace Elib.Digital.Domain;

/// <summary>
/// Bản sao bạn đọc theo event của patron (ADR-007) — tra loại bạn đọc để áp <c>PolicyDigital</c>/<c>PolicyDigitalByCollection</c>,
/// hiện tên/liên hệ trên lượt mượn và khi báo "sách đã sẵn sàng"/"hết hạn mượn" qua notification. Không có bên ELIB-LRC (monolith đọc
/// thẳng bảng Reader) — service riêng nên cần bản sao như circulation đã làm (<c>Elib.Circulation.Application/Replicas.cs</c>).
/// </summary>
public sealed class PatronReplica : Entity, ITenantOwned
{
    public long TenantId { get; set; }
    public Guid ReaderPublicId { get; set; }
    public string CardNo { get; set; } = "";
    public string FullName { get; set; } = "";
    public long? ReaderTypeId { get; set; }
    public string? ReaderTypeName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool Deleted { get; set; }
    public long Version { get; set; }

    public static string Key(string cardNo) => (cardNo ?? "").Trim().ToUpperInvariant();
}
