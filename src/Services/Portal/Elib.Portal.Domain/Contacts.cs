using Elib.BuildingBlocks.Domain;

namespace Elib.Portal.Domain;

/// <summary>Nhóm liên hệ (LRC: ContactGroup) — mỗi nhóm có email nhận thông báo riêng (ví dụ "Hỏi đáp mượn sách").</summary>
public sealed class ContactGroup : TenantEntity, IHasStatus
{
    private ContactGroup() { }

    public string Name { get; private set; } = "";
    public string? Email { get; private set; }
    public string? Description { get; private set; }
    public string? Language { get; private set; }
    public int SortOrder { get; private set; }
    public int Status { get; private set; } = IHasStatus.Active;

    public static ContactGroup Create(string? name, string? email, string? description, string? language, int sortOrder, int status)
    {
        var group = new ContactGroup();
        group.Update(name, email, description, language, sortOrder, status);
        return group;
    }

    public void Update(string? name, string? email, string? description, string? language, int sortOrder, int status)
    {
        Name = TextRules.Required(name, 200, "NAME_INVALID", "Tên nhóm liên hệ");
        Email = TextRules.Optional(email, 200, "EMAIL_INVALID", "Email nhận");
        Description = TextRules.Optional(description, 500, "DESCRIPTION_INVALID", "Mô tả");
        Language = TextRules.Language(language);
        SortOrder = sortOrder;
        Status = StatusRules.Validate(status);
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);
}

/// <summary>
/// Liên hệ gửi từ OPAC (LRC: Contact) — ẩn danh. Status: 1 Mới, 2 Đã xử lý (IHasStatus: Inactive/Active). Giới hạn độ dài chặt; chống spam
/// (honeypot, rate limit, trần số liên hệ chưa xử lý) ở tầng ứng dụng/gateway.
/// </summary>
public sealed class Contact : TenantEntity, IHasStatus
{
    public const int MaxContentLength = 4000;

    private Contact() { }

    public long? ContactGroupId { get; private set; }
    public string Name { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string? Mobile { get; private set; }
    public string? Address { get; private set; }
    public string? Title { get; private set; }
    public string Content { get; private set; } = "";
    public int Status { get; private set; } = IHasStatus.Inactive;
    public DateTimeOffset? HandledAt { get; private set; }
    public long? HandledBy { get; private set; }
    public string? HandleNote { get; private set; }

    public static Contact Submit(long? groupId, string? name, string? email, string? mobile, string? address, string? title, string? content)
    {
        var contact = new Contact { ContactGroupId = groupId };
        contact.Name = TextRules.Required(name, 200, "NAME_INVALID", "Họ tên");
        contact.Email = TextRules.Required(email, 200, "EMAIL_INVALID", "Email");
        if (!contact.Email.Contains('@', StringComparison.Ordinal) || contact.Email.Contains(' ', StringComparison.Ordinal))
            throw new BusinessRuleException("EMAIL_INVALID", "Email không hợp lệ.");
        contact.Mobile = TextRules.Optional(mobile, 30, "MOBILE_INVALID", "Số điện thoại");
        contact.Address = TextRules.Optional(address, 300, "ADDRESS_INVALID", "Địa chỉ");
        contact.Title = TextRules.Optional(title, 250, "TITLE_INVALID", "Tiêu đề");
        contact.Content = TextRules.Required(content, MaxContentLength, "CONTENT_INVALID", "Nội dung");
        return contact;
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);

    public void MarkHandled(long? actorId, DateTimeOffset now, string? note)
    {
        Status = IHasStatus.Active;
        HandledAt = now;
        HandledBy = actorId;
        HandleNote = TextRules.Optional(note, 500, "NOTE_INVALID", "Ghi chú xử lý");
    }
}

/// <summary>
/// Bộ đếm truy cập (LRC: Counter, mỗi dòng một lượt kèm IP) — K12 cộng dồn theo ngày, không lưu IP: dữ liệu cá nhân không cần cho con số
/// "bạn là người truy cập thứ N". Ngày tính theo giờ Việt Nam.
/// </summary>
public sealed class CounterDay : TenantEntity
{
    private CounterDay() { }

    public DateOnly Day { get; private set; }
    public long Hits { get; private set; }

    public static CounterDay For(DateOnly day) => new() { Day = day };

    public void Add() => Hits++;
}
