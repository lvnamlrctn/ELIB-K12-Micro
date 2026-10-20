using Elib.BuildingBlocks.Domain;

namespace Elib.Space.Domain;

/// <summary>Bản sao bạn đọc từ patron (ReaderChanged) — khuôn digital's PatronReplica, chỉ trường cần cho đặt phòng.</summary>
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
