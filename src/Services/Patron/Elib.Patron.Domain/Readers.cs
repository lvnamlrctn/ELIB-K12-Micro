using System.Text.RegularExpressions;
using Elib.BuildingBlocks.Domain;

namespace Elib.Patron.Domain;

/// <summary>Loại bạn đọc — học sinh, giáo viên… (monolith: dbo.ReaderType). Chính sách lưu thông gắn theo loại.</summary>
public sealed class ReaderType : NamedCatalogItem
{
    public override int MaxNameLength => 250;
}

/// <summary>Lớp (monolith: dbo.Class).</summary>
public sealed class SchoolClass : NamedCatalogItem
{
    public override int MaxNameLength => 250;
}

/// <summary>Khoá / niên khoá (monolith: dbo.Course).</summary>
public sealed class Course : NamedCatalogItem
{
    public override int MaxNameLength => 250;
}

/// <summary>Nhóm bạn đọc (monolith: dbo.GroupReader) — có mã tuỳ chọn.</summary>
public sealed class ReaderGroup : TenantEntity
{
    private ReaderGroup() { }

    public string Name { get; private set; } = "";
    public string? Code { get; private set; }

    public static ReaderGroup Create(string name, string? code)
    {
        var group = new ReaderGroup();
        group.Update(name, code);
        return group;
    }

    public void Update(string name, string? code)
    {
        var value = (name ?? "").Trim();
        Name = value.Length is > 0 and <= 250 ? value : throw new BusinessRuleException("NAME_INVALID", "Tên nhóm bắt buộc, tối đa 250 ký tự.");
        var c = string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
        Code = c is null or { Length: <= 50 } ? c : throw new BusinessRuleException("CODE_INVALID", "Mã nhóm tối đa 50 ký tự.");
    }
}

/// <summary>Giới tính theo quy ước monolith: 1 = nam, 0 = nữ, null = không rõ.</summary>
public static class Sex
{
    public const int Female = 0;
    public const int Male = 1;
}

/// <summary>Dữ liệu nhập của bạn đọc (Thêm / Sửa / nhập Excel) — gom lại để kiểm tra một chỗ.</summary>
public sealed record ReaderData(
    string CardNo, string? LastName, string FirstName, string? CitizenId, string? CardUid, string? Email, string? Phone, string? Address,
    DateOnly? BirthDate, int? Sex, long? ReaderTypeId, long? ClassId, long? CourseId, long? OrgId, long? DegreeId, long? EthnicityId,
    long? AcademicTitleId, DateOnly? IssueDate, DateOnly? ExpireDate);

/// <summary>
/// Bạn đọc (monolith: dbo.Readers). Trạng thái: 2 = hoạt động, 1 = bị khoá (Lock kèm lý do) — giữ quy ước Status của monolith.
/// Mật khẩu đăng nhập OPAC KHÔNG nằm ở đây — tài khoản bạn đọc thuộc identity.
/// <see cref="Version"/> tăng ở mỗi thay đổi: bản sao bạn đọc ở circulation/digital… bỏ qua event cũ hơn.
/// </summary>
public sealed partial class Reader : TenantEntity, IHasStatus
{
    private Reader() { }

    public string CardNo { get; private set; } = "";
    public string? LastName { get; private set; }
    public string FirstName { get; private set; } = "";
    public string? CitizenId { get; private set; }
    public string? CardUid { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    public DateOnly? BirthDate { get; private set; }
    public int? Sex { get; private set; }
    public long? ReaderTypeId { get; private set; }
    public long? ClassId { get; private set; }
    public long? CourseId { get; private set; }

    /// <summary>Tham chiếu danh mục của service tenant (phòng ban, trình độ, dân tộc, học hàm) — chỉ lưu id.</summary>
    public long? OrgId { get; private set; }
    public long? DegreeId { get; private set; }
    public long? EthnicityId { get; private set; }
    public long? AcademicTitleId { get; private set; }

    public DateOnly? IssueDate { get; private set; }
    public DateOnly? ExpireDate { get; private set; }
    public int Status { get; private set; } = IHasStatus.Active;
    public string? LockReason { get; private set; }
    public long Version { get; private set; }

    /// <summary>Ảnh thẻ (monolith: Reader.Photo) — id file ở service media (mục đích reader-photo, bucket riêng tư).</summary>
    public Guid? PhotoId { get; private set; }

    /// <summary>"Họ đệm Tên" như monolith (LastName + FirstName).</summary>
    public string FullName => string.IsNullOrWhiteSpace(LastName) ? FirstName : $"{LastName} {FirstName}";

    public static Reader Create(ReaderData data)
    {
        var reader = new Reader();
        reader.Apply(data);
        return reader;
    }

    public void Update(ReaderData data)
    {
        Apply(data);
        Version++;
    }

    public void ChangeStatus(int status)
    {
        Status = StatusRules.Validate(status);
        if (Status == IHasStatus.Active) LockReason = null;
        Version++;
    }

    /// <summary>Khoá thẻ (monolith: Reader/Lock) — bạn đọc không mượn được, OPAC không đăng nhập được.</summary>
    public void Lock(string? reason)
    {
        Status = IHasStatus.Inactive;
        LockReason = Cut(reason, 500);
        Version++;
    }

    public void Unlock() => ChangeStatus(IHasStatus.Active);

    /// <summary>Đổi/xoá ảnh thẻ. Không tăng <see cref="Version"/>: ảnh không nằm trong bản sao bạn đọc ở service khác.</summary>
    public void SetPhoto(Guid? photoId) => PhotoId = photoId == Guid.Empty ? null : photoId;

    /// <summary>Sửa hàng loạt (monolith: BulkUpdate/BatchUpdate) — chỉ các trường được gửi.</summary>
    public void ApplyBulk(long? readerTypeId, long? classId, long? courseId, DateOnly? issueDate, DateOnly? expireDate)
    {
        if (readerTypeId is not null) ReaderTypeId = readerTypeId;
        if (classId is not null) ClassId = classId;
        if (courseId is not null) CourseId = courseId;
        if (issueDate is not null) IssueDate = issueDate;
        if (expireDate is not null) ExpireDate = expireDate;
        CheckDates();
        Version++;
    }

    private void Apply(ReaderData d)
    {
        CardNo = NormalizeCardNo(d.CardNo);
        FirstName = Required(d.FirstName, 100, "READER_NAME_INVALID", "Tên bạn đọc bắt buộc, tối đa 100 ký tự.");
        LastName = Optional(d.LastName, 150, "Họ đệm");
        CitizenId = Optional(d.CitizenId, 20, "Số CCCD");
        CardUid = NormalizeCardUid(d.CardUid);
        var email = Optional(d.Email, 250, "Email");
        Email = email is null || email.Contains('@', StringComparison.Ordinal)
            ? email
            : throw new BusinessRuleException("READER_EMAIL_INVALID", $"Email '{email}' không hợp lệ.");
        Phone = Optional(d.Phone, 30, "Số điện thoại");
        Address = Optional(d.Address, 500, "Địa chỉ");
        BirthDate = d.BirthDate;
        Sex = d.Sex is null or Domain.Sex.Female or Domain.Sex.Male
            ? d.Sex
            : throw new BusinessRuleException("READER_SEX_INVALID", "Giới tính: 1 = nam, 0 = nữ.");
        ReaderTypeId = d.ReaderTypeId;
        ClassId = d.ClassId;
        CourseId = d.CourseId;
        OrgId = d.OrgId;
        DegreeId = d.DegreeId;
        EthnicityId = d.EthnicityId;
        AcademicTitleId = d.AcademicTitleId;
        IssueDate = d.IssueDate;
        ExpireDate = d.ExpireDate;
        CheckDates();
    }

    private void CheckDates()
    {
        if (IssueDate is { } issue && ExpireDate is { } expire && expire < issue)
            throw new BusinessRuleException("READER_EXPIRE_BEFORE_ISSUE", "Ngày hết hạn thẻ phải sau ngày cấp.");
    }

    /// <summary>Số thẻ: bỏ khoảng trắng hai đầu, in hoa; chữ, số, '.', '-', '_' (in được thành mã vạch).</summary>
    public static string NormalizeCardNo(string? cardNo)
    {
        var value = (cardNo ?? "").Trim().ToUpperInvariant();
        return CardNoPattern().IsMatch(value)
            ? value
            : throw new BusinessRuleException("READER_CARDNO_INVALID", "Số thẻ bắt buộc, tối đa 50 ký tự chữ, số, '.', '-', '_'.");
    }

    /// <summary>UID thẻ chip/RFID: bỏ khoảng trắng, ':' và '-', in hoa; chữ và số, tối đa 64 ký tự (monolith: CardUid.Normalize).</summary>
    public static string? NormalizeCardUid(string? uid)
    {
        if (string.IsNullOrWhiteSpace(uid)) return null;
        var value = uid.Replace(" ", "", StringComparison.Ordinal).Replace(":", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal).ToUpperInvariant();
        return CardUidPattern().IsMatch(value)
            ? value
            : throw new BusinessRuleException("READER_CARDUID_INVALID", "UID thẻ chỉ gồm chữ và số, tối đa 64 ký tự.");
    }

    private static string Required(string? value, int max, string code, string message)
    {
        var v = (value ?? "").Trim();
        return v.Length is > 0 && v.Length <= max ? v : throw new BusinessRuleException(code, message);
    }

    private static string? Optional(string? value, int max, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        return v.Length <= max ? v : throw new BusinessRuleException("READER_FIELD_TOO_LONG", $"{label} tối đa {max} ký tự.");
    }

    private static string? Cut(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim() is var v && v.Length > max ? v[..max] : value.Trim();

    [GeneratedRegex("^[A-Z0-9._-]{1,50}$")]
    private static partial Regex CardNoPattern();

    [GeneratedRegex("^[A-Z0-9]{1,64}$")]
    private static partial Regex CardUidPattern();
}
