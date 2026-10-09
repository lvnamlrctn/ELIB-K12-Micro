namespace ELIBAPI.Core.DTOs.Request;

/// <summary>Danh sách kỳ ấn phẩm của một đăng ký.</summary>
public class SerialReceiptSearchRequest
{
    public long? SubscriptionId { get; set; }
    public int?  Status         { get; set; }
    public int?  PageIndex      { get; set; }
    public int?  PageSize       { get; set; }
    /// <summary>Giữ tương thích với giao diện cũ — phạm vi đơn vị nay lấy theo đăng ký.</summary>
    public Guid? TenantId       { get; set; }
}

public class ClaimRequest { public long Id { get; set; } }

public class PredictIssuesRequest
{
    public long? SubscriptionId { get; set; }
    public int?  Count          { get; set; }
}

/// <summary>
/// Thêm / sửa / nhận một kỳ ấn phẩm (màn "Nhận kỳ"). Tên trường theo đúng JSON giao diện gửi lên
/// (<c>subscriptionId</c>, <c>serialSeqX</c>, <c>plannedDate</c>…). <see cref="Id"/> hoặc <see cref="PublicId"/> có
/// giá trị thì sửa đúng kỳ đó; không có thì thêm kỳ mới.
/// </summary>
public class SerialIssueSaveRequest
{
    public long?     Id             { get; set; }
    public Guid?     PublicId       { get; set; }
    public long?     SubscriptionId { get; set; }
    public string?   SerialSeq      { get; set; }
    public int?      SerialSeqX     { get; set; }
    public int?      SerialSeqY     { get; set; }
    public int?      SerialSeqZ     { get; set; }
    public int?      IsSpecial      { get; set; }
    public int?      Status         { get; set; }
    public int?      Quantity       { get; set; }
    public DateTime? PlannedDate    { get; set; }
    public string?   Note           { get; set; }
    public DateTime? PublishedDate  { get; set; }
    public DateTime? ClaimDate      { get; set; }
    public int?      ClaimCount     { get; set; }
    public bool?     IsMerged       { get; set; }
    public int?      SortOrder      { get; set; }
}
