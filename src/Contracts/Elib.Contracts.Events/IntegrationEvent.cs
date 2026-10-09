namespace Elib.Contracts.Events;

/// <summary>
/// Envelope bắt buộc của mọi integration event (docs 03 §3.1).
/// Quy tắc tiến hoá: CHỈ thêm field (có giá trị mặc định); đổi nghĩa thì tạo event V2 chạy song song.
/// </summary>
public abstract record IntegrationEvent
{
    /// <summary>Khoá chống trùng của consumer (inbox). Publisher không tự đặt — mặc định Guid v7.</summary>
    public Guid EventId { get; init; } = Guid.CreateVersion7();

    /// <summary>Đơn vị phát sinh sự kiện. Consumer chạy trong ngữ cảnh đơn vị này.</summary>
    public required long TenantId { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public EventActor Actor { get; init; } = EventActor.System;
}

/// <summary>
/// Sự kiện cấp nền tảng không thuộc một đơn vị (ví dụ quyền của tài khoản super-admin).
/// Consumer chạy trong ngữ cảnh hệ thống — chỉ dùng khi thật sự cần.
/// </summary>
public abstract record SystemEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public EventActor Actor { get; init; } = EventActor.System;
}

/// <summary>Ai gây ra sự kiện. Kind: staff | reader | device | service | system.</summary>
public sealed record EventActor(long? Id, string Kind)
{
    public static readonly EventActor System = new(null, "system");
}
