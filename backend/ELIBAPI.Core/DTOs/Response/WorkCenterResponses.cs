namespace ELIBAPI.Core.DTOs.Response;

public class WorkSourceInfo
{
    public string Type     { get; set; } = "";
    public string Label    { get; set; } = "";
    public bool   CanView  { get; set; }
    public bool   CanEdit  { get; set; }
}

/// <summary>1 dòng trong danh sách/chi tiết Trung tâm công việc — hợp nhất 3 nguồn khác cấu trúc
/// (RoomBooking/DocumentSubmission/Review) về 1 hình dạng chung cho giao diện.</summary>
public class WorkItemRow
{
    public string    Type                    { get; set; } = "";
    public Guid      PublicId                { get; set; }
    public string    Title                   { get; set; } = "";
    public string?   Detail                  { get; set; }
    public string?   SubmittedByName         { get; set; }
    public string?   SubmittedByCardNo       { get; set; }
    public DateTime? SubmittedAt             { get; set; }
    public string    StatusLabel             { get; set; } = "";

    public long?     AssigneeId              { get; set; }
    public string?   AssigneeName            { get; set; }
    public bool      AssigneePermissionLost  { get; set; }
    public DateTime? DueAtUtc                { get; set; }
    public bool      Overdue                 { get; set; }
    public int       Version                 { get; set; }

    // Riêng đặt phòng — FE dùng để cảnh báo hạn muộn hơn giờ bắt đầu, và mở đúng dialog duyệt/từ chối cũ.
    public DateTime? RoomStartAt             { get; set; }
    public DateTime? RoomEndAt               { get; set; }

    // Riêng đánh giá
    public int?       Rating                 { get; set; }

    // Riêng tài liệu nộp
    public string?    DocType                { get; set; }
    public string?    FileName               { get; set; }
}

public class WorkItemsResponse
{
    public List<WorkItemRow> Items           { get; set; } = new();
    public int                TotalCount     { get; set; }
    public int                MineCount      { get; set; }
    public int                UnassignedCount { get; set; }
    public int                OverdueCount   { get; set; }
    public DateTime           CheckedAtUtc   { get; set; } = DateTime.UtcNow;
}

public class WorkItemAssigneeOption
{
    public long   Id       { get; set; }
    public string Name     { get; set; } = "";
}
