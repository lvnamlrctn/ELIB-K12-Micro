namespace ELIBAPI.Core.DTOs.Request;

/// <summary>DTO cho Trung tâm công việc (Đợt 15). Xem
/// ELIBAPI.API/Controllers/Dbo/WorkCenterController.cs.</summary>
public class WorkItemAssignmentRequest
{
    /// <summary>Version hiện đang xem trên giao diện — 0 nếu hồ sơ chưa từng có dòng metadata (chưa phân
    /// công lần nào). Dùng compare-and-swap, sai → 409.</summary>
    public int     Version     { get; set; }
    public long?   AssigneeId  { get; set; }
    public DateTime? DueAtUtc  { get; set; }
    public string  Reason      { get; set; } = "";
}

public class WorkItemClaimRequest
{
    public int Version { get; set; }
}

public class WorkItemDecisionRequest
{
    /// <summary>true = duyệt. false chỉ hợp lệ với submission (từ chối) — review "bản đầu" chỉ có duyệt.</summary>
    public bool   Approve { get; set; }
    public string Reason  { get; set; } = "";
}
