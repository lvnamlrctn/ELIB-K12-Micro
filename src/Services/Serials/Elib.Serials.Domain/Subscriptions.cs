using Elib.BuildingBlocks.Domain;

namespace Elib.Serials.Domain;

public static class SubscriptionStatus
{
    public const int Draft = 0;
    public const int Active = 1;
    public const int Closed = 2;

    public static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string> { [Draft] = "Nháp", [Active] = "Đang đặt", [Closed] = "Đã đóng" };
}

public sealed record SubscriptionData(
    string? Title, string? Issn = null, Guid? BibPublicId = null, long? BibMfn = null, long? PublicationTypeId = null, long? FrequencyId = null,
    long? PatternId = null, long? StoreId = null, string? StoreName = null, Guid? SupplierPublicId = null, string? SupplierName = null,
    DateOnly? StartsOn = null, DateOnly? EndsOn = null, DateOnly? FirstIssueDate = null, int? StartX = null, int? StartY = null, int? StartZ = null,
    string? CallNumber = null, string? PublicNote = null, string? InternalNote = null, int GraceDays = 0, int Status = SubscriptionStatus.Active);

/// <summary>
/// Đăng ký đặt báo / tạp chí (monolith: Serial, quyền SUBSCRIPTIONS). Gắn một biểu ghi thư mục (<see cref="BibPublicId"/>) hoặc chỉ khai nhan đề + ISSN;
/// đóng tập cần biểu ghi. Đã duyệt thì khoá sửa/xoá.
/// </summary>
public sealed class Subscription : TenantEntity
{
    private Subscription() { }

    public Guid? BibPublicId { get; private set; }
    public long? BibMfn { get; private set; }
    public string Title { get; private set; } = "";
    public string? Issn { get; private set; }
    public long? PublicationTypeId { get; private set; }
    public long? FrequencyId { get; private set; }
    public long? PatternId { get; private set; }
    public long? StoreId { get; private set; }
    public string? StoreName { get; private set; }
    public Guid? SupplierPublicId { get; private set; }
    public string? SupplierName { get; private set; }
    public DateOnly? StartsOn { get; private set; }
    public DateOnly? EndsOn { get; private set; }

    /// <summary>Ngày phát hành kỳ đầu — mốc tính ngày dự kiến.</summary>
    public DateOnly? FirstIssueDate { get; private set; }

    public int? StartX { get; private set; }
    public int? StartY { get; private set; }
    public int? StartZ { get; private set; }

    /// <summary>Số kỳ cuối đã sinh dự kiến (mốc sinh tiếp).</summary>
    public int? LastX { get; private set; }

    public int? LastY { get; private set; }
    public int? LastZ { get; private set; }
    public string? CallNumber { get; private set; }
    public string? PublicNote { get; private set; }
    public string? InternalNote { get; private set; }

    /// <summary>Số ngày chờ sau ngày dự kiến trước khi khiếu nại.</summary>
    public int GraceDays { get; private set; }

    public int Status { get; private set; } = SubscriptionStatus.Active;
    public bool Approved { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public long? ApprovedBy { get; private set; }

    public static Subscription Create(SubscriptionData data)
    {
        var s = new Subscription();
        s.Apply(data);
        return s;
    }

    public void Update(SubscriptionData data)
    {
        EnsureEditable();
        Apply(data);
    }

    public void EnsureEditable()
    {
        if (Approved) throw new BusinessRuleException("SUBSCRIPTION_APPROVED", "Đăng ký đã duyệt, không sửa/xoá được.");
    }

    private void Apply(SubscriptionData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Title = TextRules.Required(data.Title, 500, "TITLE_INVALID", "Nhan đề");
        Issn = TextRules.Optional(data.Issn, 50, "ISSN_INVALID", "ISSN");
        BibPublicId = data.BibPublicId;
        BibMfn = data.BibMfn;
        PublicationTypeId = data.PublicationTypeId;
        FrequencyId = data.FrequencyId;
        PatternId = data.PatternId;
        StoreId = data.StoreId;
        StoreName = TextRules.Optional(data.StoreName, 250, "STORE_NAME_INVALID", "Tên kho");
        SupplierPublicId = data.SupplierPublicId;
        SupplierName = TextRules.Optional(data.SupplierName, 250, "SUPPLIER_NAME_INVALID", "Nhà cung cấp");
        if (data.StartsOn is { } from && data.EndsOn is { } to && to < from)
            throw new BusinessRuleException("PERIOD_INVALID", "Ngày kết thúc phải sau ngày bắt đầu.");
        StartsOn = data.StartsOn;
        EndsOn = data.EndsOn;
        FirstIssueDate = data.FirstIssueDate;
        StartX = data.StartX;
        StartY = data.StartY;
        StartZ = data.StartZ;
        CallNumber = TextRules.Optional(data.CallNumber, 100, "CALL_NUMBER_INVALID", "Ký hiệu xếp giá");
        PublicNote = TextRules.Optional(data.PublicNote, 500, "NOTE_INVALID", "Ghi chú công khai");
        InternalNote = TextRules.Optional(data.InternalNote, 500, "NOTE_INVALID", "Ghi chú nội bộ");
        GraceDays = data.GraceDays is >= 0 and <= 365 ? data.GraceDays : throw new BusinessRuleException("GRACE_DAYS_INVALID", "Số ngày chờ khiếu nại 0–365.");
        Status = SubscriptionStatus.Names.ContainsKey(data.Status) ? data.Status : throw new BusinessRuleException("STATUS_INVALID", "Trạng thái đăng ký không hợp lệ.");
    }

    /// <summary>Gắn biểu ghi thư mục (chọn từ danh sách) — cũng không đổi được sau khi duyệt.</summary>
    public void LinkBib(Guid bibPublicId, long mfn)
    {
        EnsureEditable();
        BibPublicId = bibPublicId;
        BibMfn = mfn;
    }

    /// <summary>Duyệt (một chiều): cần biểu ghi thư mục (đóng tập tạo ĐKCB theo biểu ghi).</summary>
    public void Approve(DateTimeOffset now, long? by)
    {
        if (Approved) throw new BusinessRuleException("SUBSCRIPTION_APPROVED", "Đăng ký đã duyệt.");
        if (BibMfn is null) throw new BusinessRuleException("SERIAL_BIB_REQUIRED", "Liên kết đăng ký với một biểu ghi thư mục trước khi duyệt.");
        Approved = true;
        ApprovedAt = now;
        ApprovedBy = by;
        if (Status == SubscriptionStatus.Draft) Status = SubscriptionStatus.Active;
    }

    public void RecordLast(IssueNumber n)
    {
        LastX = n.X;
        LastY = n.Y;
        LastZ = n.Z;
    }
}

public static class IssueStatus
{
    /// <summary>Dự kiến (chưa nhận).</summary>
    public const int Planned = 0;

    public const int Received = 1;
    public const int Claimed = 2;
    public const int Missing = 3;
}

public sealed record IssueData(
    string? Label, int? X = null, int? Y = null, int? Z = null, bool IsSpecial = false, int? Quantity = null, DateOnly? PlannedDate = null,
    DateOnly? ReceivedDate = null, int? Status = null, string? Note = null);

/// <summary>Kỳ / số báo (monolith: SerialItem).</summary>
public sealed class SerialIssue : TenantEntity
{
    private SerialIssue() { }

    public long SubscriptionId { get; private set; }
    public string Label { get; private set; } = "";
    public int? X { get; private set; }
    public int? Y { get; private set; }
    public int? Z { get; private set; }
    public bool IsSpecial { get; private set; }

    /// <summary>Kỳ ghép từ nhiều số (nhãn "3+4").</summary>
    public bool IsMerged { get; private set; }

    public int Status { get; private set; }
    public int Quantity { get; private set; } = 1;
    public DateOnly? PlannedDate { get; private set; }
    public DateOnly? ReceivedDate { get; private set; }
    public DateTimeOffset? ClaimDate { get; private set; }
    public int ClaimCount { get; private set; }
    public int SortOrder { get; private set; }
    public string? Note { get; private set; }

    public static SerialIssue Create(long subscriptionId, IssueData data, bool merged = false)
    {
        var issue = new SerialIssue { SubscriptionId = subscriptionId, IsMerged = merged };
        issue.Apply(data);
        return issue;
    }

    /// <summary>Áp dữ liệu (thêm/sửa/nhận). Trường null giữ giá trị cũ; nhận kỳ mà chưa có ngày nhận thì lấy <paramref name="today"/>.</summary>
    public void Apply(IssueData data, DateOnly? today = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.X is not null) X = data.X;
        if (data.Y is not null) Y = data.Y;
        if (data.Z is not null) Z = data.Z;
        if (data.Label is { } label) Label = TextRules.Required(label, 250, "LABEL_INVALID", "Nhãn kỳ");
        else if (Label.Length == 0) throw new BusinessRuleException("LABEL_INVALID", "Nhãn kỳ bắt buộc.");
        IsSpecial = data.IsSpecial;
        if (data.Quantity is { } q) Quantity = q is >= 1 and <= 1000 ? q : throw new BusinessRuleException("QUANTITY_INVALID", "Số bản mỗi kỳ 1–1000.");
        if (data.PlannedDate is not null) PlannedDate = data.PlannedDate;
        if (data.ReceivedDate is not null) ReceivedDate = data.ReceivedDate;
        if (data.Note is not null) Note = TextRules.Optional(data.Note, 500, "NOTE_INVALID", "Ghi chú");
        if (data.Status is { } status)
        {
            if (status is < IssueStatus.Planned or > IssueStatus.Missing) throw new BusinessRuleException("STATUS_INVALID", "Trạng thái kỳ 0–3.");
            Status = status;
        }
        if (Status == IssueStatus.Received) ReceivedDate ??= today ?? VietnamDate.Of(DateTimeOffset.UtcNow);
    }

    public void SetOrder(int order) => SortOrder = order;

    public void Receive(DateOnly date, int? quantity = null)
    {
        Status = IssueStatus.Received;
        ReceivedDate = date;
        if (quantity is { } q) Quantity = q is >= 1 and <= 1000 ? q : throw new BusinessRuleException("QUANTITY_INVALID", "Số bản mỗi kỳ 1–1000.");
    }

    public void Claim(DateTimeOffset now)
    {
        if (Status == IssueStatus.Received) throw new BusinessRuleException("ISSUE_RECEIVED", "Kỳ đã nhận, không khiếu nại được.");
        Status = IssueStatus.Claimed;
        ClaimDate = now;
        ClaimCount++;
    }
}

public sealed record BindingData(string? AccessionNo, string? VolumeTitle, long SubscriptionId, DateOnly? BindingDate = null, long? StoreId = null, string? StoreName = null,
    string? Note = null);

/// <summary>Tập đóng (monolith: SerialBinding): gom các kỳ đã nhận thành một tập có ĐKCB ở Holdings.</summary>
public sealed class Binding : TenantEntity
{
    private Binding() { }

    public string AccessionNo { get; private set; } = "";
    public string VolumeTitle { get; private set; } = "";
    public long SubscriptionId { get; private set; }
    public DateOnly BindingDate { get; private set; }
    public long? StoreId { get; private set; }
    public string? StoreName { get; private set; }
    public string? Note { get; private set; }

    /// <summary>Bản sách (ĐKCB) đã tạo ở Holdings.</summary>
    public Guid? ItemPublicId { get; private set; }

    public static Binding Create(Guid publicId, BindingData data, DateOnly today)
    {
        var b = new Binding { PublicId = publicId };
        b.Apply(data, today);
        return b;
    }

    public void Apply(BindingData data, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(data);
        AccessionNo = TextRules.Required(data.AccessionNo, 50, "ACCESSION_NO_INVALID", "Số ĐKCB");
        VolumeTitle = TextRules.Required(data.VolumeTitle, 500, "VOLUME_TITLE_INVALID", "Nhan đề tập");
        SubscriptionId = data.SubscriptionId;
        BindingDate = data.BindingDate ?? today;
        StoreId = data.StoreId;
        StoreName = TextRules.Optional(data.StoreName, 250, "STORE_NAME_INVALID", "Tên kho");
        Note = TextRules.Optional(data.Note, 500, "NOTE_INVALID", "Ghi chú");
    }

    public void AttachItem(Guid itemPublicId) => ItemPublicId = itemPublicId;
}

/// <summary>Kỳ thuộc một tập đóng — mỗi kỳ chỉ thuộc một tập.</summary>
public sealed class BindingIssue : TenantEntity
{
    private BindingIssue() { }

    public long BindingId { get; private set; }
    public long IssueId { get; private set; }

    public static BindingIssue Create(long bindingId, long issueId) => new() { BindingId = bindingId, IssueId = issueId };
}
