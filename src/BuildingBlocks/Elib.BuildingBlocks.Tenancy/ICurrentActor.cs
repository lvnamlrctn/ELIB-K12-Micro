namespace Elib.BuildingBlocks.Tenancy;

/// <summary>Ai đang thực hiện thao tác — dùng cho cột audit và nhật ký. Scoped.</summary>
public interface ICurrentActor
{
    /// <summary>Id người dùng/bạn đọc/thiết bị theo claim sub. null với job hệ thống hoặc request ẩn danh.</summary>
    long? Id { get; }

    /// <summary>staff | reader | device | service | system | anonymous</summary>
    string Kind { get; }
}

public sealed class CurrentActor : ICurrentActor
{
    public long? Id { get; private set; }
    public string Kind { get; private set; } = "anonymous";

    public void Set(long? id, string kind)
    {
        Id = id;
        Kind = kind;
    }
}
