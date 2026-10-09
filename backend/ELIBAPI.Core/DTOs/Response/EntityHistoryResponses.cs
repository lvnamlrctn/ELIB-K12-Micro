namespace ELIBAPI.Core.DTOs.Response;

public class EntityHistoryFieldChange
{
    public string Field { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

public class EntityHistoryEvent
{
    public long Id { get; set; }
    public DateTime? Timestamp { get; set; }
    public long ActorId { get; set; }
    public string? ActorName { get; set; }
    public string Action { get; set; } = "";
    public string? Reason { get; set; }
    public List<EntityHistoryFieldChange> Changes { get; set; } = [];
}

public class EntityHistoryResponse
{
    public List<EntityHistoryEvent> Items { get; set; } = [];
    public long? Next { get; set; }
    /// <summary>Đợt 21: người đã thao tác trên CHÍNH hồ sơ này (cho ô lọc "Người thao tác") — chỉ trả ở trang đầu
    /// (không có <c>before</c>); null ở các trang sau.</summary>
    public List<EntityHistoryActor>? Actors { get; set; }
}

public class EntityHistoryActor
{
    public long Id { get; set; }
    public string? Name { get; set; }
}
