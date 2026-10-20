using Elib.BuildingBlocks.Domain;

namespace Elib.Space.Domain;

public static class RoomBookingBanSource
{
    public const int Auto = 1;
    public const int Staff = 2;
}

/// <summary>
/// Cấm đặt phòng (monolith: map.RoomBookingBan, quyền SPACE_BANS) — tự động sau nhiều lần vắng mặt hoặc thủ thư cấm tay. Gỡ là
/// xoá mềm nghiệp vụ (LiftedAt), không xoá dòng — giữ lịch sử. Khác bản gốc: cấm theo đơn vị (TenantId), không toàn cục.
/// </summary>
public sealed class RoomBookingBan : TenantEntity
{
    public const int MaxReasonLength = 500;

    private RoomBookingBan() { }

    public Guid ReaderPublicId { get; private set; }
    public string CardNo { get; private set; } = "";
    public string Reason { get; private set; } = "";
    public int Source { get; private set; } = RoomBookingBanSource.Staff;
    public DateTimeOffset BannedUntil { get; private set; }
    public DateTimeOffset? LiftedAt { get; private set; }
    public long? LiftedBy { get; private set; }

    public bool IsActive(DateTimeOffset now) => LiftedAt is null && BannedUntil > now;

    public static RoomBookingBan Create(Guid readerPublicId, string cardNo, string reason, int source, DateTimeOffset bannedUntil)
    {
        var r = (reason ?? "").Trim();
        if (r.Length is 0 or > MaxReasonLength) throw new BusinessRuleException("REASON_REQUIRED", $"Nhập lý do cấm đặt (tối đa {MaxReasonLength} ký tự).");
        return new RoomBookingBan
        {
            ReaderPublicId = readerPublicId, CardNo = (cardNo ?? "").Trim().ToUpperInvariant(), Reason = r, Source = source, BannedUntil = bannedUntil,
        };
    }

    public void Lift(long? staffId, DateTimeOffset now)
    {
        if (LiftedAt is not null) throw new BusinessRuleException("BAN_ALREADY_LIFTED", "Lệnh cấm đã được gỡ trước đó.");
        LiftedAt = now;
        LiftedBy = staffId;
    }
}
