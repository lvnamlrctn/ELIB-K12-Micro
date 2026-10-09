using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Patron;
using Elib.Patron.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Elib.Patron.Application;

/// <summary>Tìm bạn đọc (monolith: ReaderSearchRequest). Keyword tìm trong số thẻ, họ tên, email, điện thoại, CCCD.</summary>
public sealed class ReaderSearch : CrudSearch
{
    public string? CardNo { get; set; }
    public long? ReaderTypeId { get; set; }
    public long? ClassId { get; set; }
    public long? CourseId { get; set; }
    public long? OrgId { get; set; }
    public DateOnly? IssuedFrom { get; set; }
    public DateOnly? IssuedTo { get; set; }
    public DateOnly? ExpiredFrom { get; set; }
    public DateOnly? ExpiredTo { get; set; }

    /// <summary>true = chỉ thẻ đã hết hạn (trước hôm nay), false = còn hạn hoặc không thời hạn.</summary>
    public bool? Expired { get; set; }
}

/// <summary>
/// Thêm/sửa bạn đọc. Ngày dạng yyyy-MM-dd. <see cref="ReaderTypeName"/>/<see cref="ClassName"/>/<see cref="CourseName"/>:
/// thay cho id khi nhập Excel (tra theo tên trong danh mục của đơn vị).
/// </summary>
public sealed record ReaderRequest(
    string CardNo, string? LastName, string FirstName, string? CitizenId = null, string? CardUid = null, string? Email = null,
    string? Phone = null, string? Address = null, DateOnly? BirthDate = null, int? Sex = null, long? ReaderTypeId = null,
    long? ClassId = null, long? CourseId = null, long? OrgId = null, long? DegreeId = null, long? EthnicityId = null,
    long? AcademicTitleId = null, DateOnly? IssueDate = null, DateOnly? ExpireDate = null, int? Status = null,
    string? ReaderTypeName = null, string? ClassName = null, string? CourseName = null);

/// <summary>Danh mục chỉ trả id — app Admin ghép tên từ danh mục đã tải (loại, lớp, khoá; phòng ban… ở service tenant).</summary>
public sealed record ReaderDto(
    long Id, Guid PublicId, string CardNo, string? LastName, string FirstName, string FullName, string? CitizenId, string? CardUid,
    string? Email, string? Phone, string? Address, DateOnly? BirthDate, int? Sex, long? ReaderTypeId, long? ClassId, long? CourseId,
    long? OrgId, long? DegreeId, long? EthnicityId, long? AcademicTitleId, DateOnly? IssueDate, DateOnly? ExpireDate,
    int Status, string? LockReason, DateTimeOffset CreatedAt, Guid? PhotoId);

public sealed record LockReaderRequest(string? Reason);

/// <summary>Sửa hàng loạt (monolith: BulkUpdate/BatchUpdate) — chỉ trường được gửi; Status 1/2 = khoá/mở.</summary>
public sealed record ReaderBulkUpdateRequest(
    IReadOnlyList<Guid> PublicIds, long? ReaderTypeId = null, long? ClassId = null, long? CourseId = null,
    DateOnly? IssueDate = null, DateOnly? ExpireDate = null, int? Status = null);

public sealed record CardNoCheck(bool Exists);

/// <summary>Ảnh thẻ: id file đã upload xong ở media (mục đích reader-photo); null = xoá ảnh.</summary>
public sealed record ReaderPhotoRequest(Guid? FileId);

/// <summary>Gán ảnh hàng loạt theo số thẻ (monolith: UploadPhotosZip — app Admin giải nén, upload từng ảnh lên media rồi gửi danh sách).</summary>
public sealed record ReaderPhotoAssignment(string CardNo, Guid FileId);

public sealed record ReaderPhotosRequest(IReadOnlyList<ReaderPhotoAssignment> Items);

/// <summary>matched: số bạn đọc đã đổi ảnh; notFound: số thẻ không có trong đơn vị.</summary>
public sealed record ReaderPhotosResult(int Matched, IReadOnlyList<string> NotFound);

/// <summary>Xuất Excel (monolith: Reader/Export) — điều kiện lọc như màn danh sách, Fields rỗng = mọi trường.</summary>
public sealed record ReaderExportRequest(ReaderSearch? Search, IReadOnlyList<string>? Fields);

/// <summary>
/// Bạn đọc (monolith: ReaderController). Ngoài 8 endpoint chuẩn: Lock/Unlock, CheckExist, BulkUpdate, ImportTemplate/Import.
/// Mọi thay đổi phát <see cref="ReaderChanged"/> (outbox, cùng transaction) cho bản sao ở các service khác.
/// </summary>
public sealed class ReaderResource(ICrudDbContext db, IPublishEndpoint publisher, ITenantContext tenant, ICurrentActor actor, TimeProvider clock)
    : CrudResource<ReaderResource, Reader, ReaderSearch, ReaderRequest, ReaderDto>(db), ICrudImportable<ReaderRequest>
{
    public const int MaxBulk = 2000;

    protected override string EntityName => "Bạn đọc";

    protected override string? Describe(Reader entity) => $"{entity.CardNo} — {entity.FullName}";

    protected override Expression<Func<Reader, ReaderDto>> Projection => x => new ReaderDto(
        x.Id, x.PublicId, x.CardNo, x.LastName, x.FirstName,
        x.LastName == null || x.LastName == "" ? x.FirstName : x.LastName + " " + x.FirstName,
        x.CitizenId, x.CardUid, x.Email, x.Phone, x.Address, x.BirthDate, x.Sex, x.ReaderTypeId, x.ClassId, x.CourseId,
        x.OrgId, x.DegreeId, x.EthnicityId, x.AcademicTitleId, x.IssueDate, x.ExpireDate, x.Status, x.LockReason, x.CreatedAt, x.PhotoId);

    protected override Reader Create(ReaderRequest request)
    {
        var reader = Reader.Create(ToData(request));
        if (request.Status is { } status && status != IHasStatus.Active) reader.ChangeStatus(status);
        return reader;
    }

    /// <summary>Tra tên loại/lớp/khoá (nhập Excel) sang id trước khi tạo.</summary>
    protected override async Task<Reader> CreateAsync(ReaderRequest request, CancellationToken ct) =>
        Create(request with
        {
            ReaderTypeId = request.ReaderTypeId ?? await IdByNameAsync<ReaderType>(request.ReaderTypeName, "Loại bạn đọc", ct),
            ClassId = request.ClassId ?? await IdByNameAsync<SchoolClass>(request.ClassName, "Lớp", ct),
            CourseId = request.CourseId ?? await IdByNameAsync<Course>(request.CourseName, "Khoá", ct),
        });

    protected override void Update(Reader entity, ReaderRequest request)
    {
        entity.Update(ToData(request));
        if (request.Status is { } status && status != entity.Status) entity.ChangeStatus(status);
    }

    protected override IQueryable<Reader> Filter(IQueryable<Reader> query, ReaderSearch s)
    {
        if (!string.IsNullOrWhiteSpace(s.CardNo))
        {
            var cardNo = s.CardNo.Trim().ToUpperInvariant();
            query = query.Where(x => x.CardNo == cardNo);
        }
        if (s.ReaderTypeId is { } type) query = query.Where(x => x.ReaderTypeId == type);
        if (s.ClassId is { } cls) query = query.Where(x => x.ClassId == cls);
        if (s.CourseId is { } course) query = query.Where(x => x.CourseId == course);
        if (s.OrgId is { } org) query = query.Where(x => x.OrgId == org);
        if (s.IssuedFrom is { } issuedFrom) query = query.Where(x => x.IssueDate >= issuedFrom);
        if (s.IssuedTo is { } issuedTo) query = query.Where(x => x.IssueDate <= issuedTo);
        if (s.ExpiredFrom is { } expiredFrom) query = query.Where(x => x.ExpireDate >= expiredFrom);
        if (s.ExpiredTo is { } expiredTo) query = query.Where(x => x.ExpireDate <= expiredTo);
        if (s.Expired is { } expired)
        {
            var today = Today();
            query = expired ? query.Where(x => x.ExpireDate < today) : query.Where(x => x.ExpireDate == null || x.ExpireDate >= today);
        }
        if (s.Term is { } term)
        {
            var upper = term.ToUpperInvariant();
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
            query = query.Where(x => x.CardNo.Contains(upper) || x.FirstName.ToLower().Contains(term)
                                     || (x.LastName != null && (x.LastName.ToLower() + " " + x.FirstName.ToLower()).Contains(term))
                                     || (x.Email != null && x.Email.ToLower().Contains(term))
                                     || (x.Phone != null && x.Phone.Contains(term))
                                     || (x.CitizenId != null && x.CitizenId.Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return query;
    }

    protected override IOrderedQueryable<Reader> Order(IQueryable<Reader> query) => query.OrderByDescending(x => x.Id);

    protected override async Task ValidateAsync(Reader entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.CardNo == entity.CardNo && x.Id != entity.Id, ct))
            throw new ConflictException("READER_CARDNO_EXISTS", $"Số thẻ {entity.CardNo} đã có.");
        if (entity.CardUid is { } uid && await Set.AnyAsync(x => x.CardUid == uid && x.Id != entity.Id, ct))
            throw new ConflictException("READER_CARDUID_EXISTS", $"UID thẻ {uid} đã gán cho bạn đọc khác.");
        await EnsureExistsAsync<ReaderType>(entity.ReaderTypeId, "Loại bạn đọc", ct);
        await EnsureExistsAsync<SchoolClass>(entity.ClassId, "Lớp", ct);
        await EnsureExistsAsync<Course>(entity.CourseId, "Khoá", ct);
    }

    protected override Task OnSavingAsync(Reader entity, CrudChange change, CancellationToken ct) =>
        PublishAsync(entity, change == CrudChange.Deleted, ct);

    public async Task<CardNoCheck> CheckExistAsync(string cardNo, Guid? excludePublicId, CancellationToken ct)
    {
        var normalized = Reader.NormalizeCardNo(cardNo);
        return new CardNoCheck(await Set.AnyAsync(x => x.CardNo == normalized && x.PublicId != excludePublicId, ct));
    }

    public Task<ReaderDto> LockAsync(Guid publicId, string? reason, CancellationToken ct) =>
        ChangeAsync(publicId, r => r.Lock(reason), ct);

    public Task<ReaderDto> UnlockAsync(Guid publicId, CancellationToken ct) => ChangeAsync(publicId, r => r.Unlock(), ct);

    public async Task<int> BulkUpdateAsync(ReaderBulkUpdateRequest request, CancellationToken ct)
    {
        var ids = request.PublicIds.Distinct().ToList();
        if (ids.Count is 0 or > MaxBulk)
            throw new BusinessRuleException("READER_BULK_SIZE", $"Chọn từ 1 đến {MaxBulk} bạn đọc mỗi lần.");
        if (request is { ReaderTypeId: null, ClassId: null, CourseId: null, IssueDate: null, ExpireDate: null, Status: null })
            throw new BusinessRuleException("READER_BULK_EMPTY", "Chưa chọn trường cần sửa.");
        await EnsureExistsAsync<ReaderType>(request.ReaderTypeId, "Loại bạn đọc", ct);
        await EnsureExistsAsync<SchoolClass>(request.ClassId, "Lớp", ct);
        await EnsureExistsAsync<Course>(request.CourseId, "Khoá", ct);

        var readers = await Set.Where(x => ids.Contains(x.PublicId)).ToListAsync(ct);
        foreach (var reader in readers)
        {
            reader.ApplyBulk(request.ReaderTypeId, request.ClassId, request.CourseId, request.IssueDate, request.ExpireDate);
            if (request.Status is { } status && status != reader.Status) reader.ChangeStatus(status);
            await PublishAsync(reader, deleted: false, ct);
        }
        if (AuditSink is not null && readers.Count > 0)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(Reader), EntityName, Guid.Empty, CrudChange.Updated,
                $"sửa hàng loạt {readers.Count} bạn đọc"), ct);
        await Db.SaveChangesAsync(ct);
        return readers.Count;
    }

    public async Task<ReaderDto> SetPhotoAsync(Guid publicId, Guid? fileId, CancellationToken ct)
    {
        var reader = await LoadAsync(publicId, ct);
        reader.SetPhoto(fileId);
        await AuditAsync(reader, CrudChange.Updated, ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(reader);
    }

    /// <summary>Số thẻ so khớp như khi lưu (in hoa, bỏ khoảng trắng); số thẻ lặp trong danh sách thì ảnh sau thắng.</summary>
    public async Task<ReaderPhotosResult> SetPhotosAsync(ReaderPhotosRequest request, CancellationToken ct)
    {
        if (request.Items.Count is 0 or > MaxBulk)
            throw new BusinessRuleException("READER_BULK_SIZE", $"Mỗi lần gán từ 1 đến {MaxBulk} ảnh.");
        var byCard = new Dictionary<string, Guid>();
        var notFound = new List<string>();
        foreach (var item in request.Items)
        {
            var card = (item.CardNo ?? "").Trim().ToUpperInvariant();
            if (card.Length is 0 or > 50 || item.FileId == Guid.Empty) notFound.Add(item.CardNo ?? "");
            else byCard[card] = item.FileId;
        }

        var cards = byCard.Keys.ToList();
        var readers = await Set.Where(x => cards.Contains(x.CardNo)).ToListAsync(ct);
        foreach (var reader in readers) reader.SetPhoto(byCard[reader.CardNo]);
        notFound.AddRange(cards.Except(readers.Select(r => r.CardNo)));
        if (AuditSink is not null && readers.Count > 0)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(Reader), EntityName, Guid.Empty, CrudChange.Updated,
                $"cập nhật ảnh {readers.Count} bạn đọc"), ct);
        await Db.SaveChangesAsync(ct);
        return new ReaderPhotosResult(readers.Count, notFound);
    }

    // ── Xuất Excel (monolith: GetExportFields / Export) — tiêu đề cột trùng file nhập nên file xuất nhập lại được ──

    private static readonly IReadOnlyList<CrudExportColumn<ReaderExportRow>> ExportColumns =
    [
        new("cardno", "Số thẻ", r => r.Reader.CardNo),
        new("lastname", "Họ đệm", r => r.Reader.LastName),
        new("firstname", "Tên", r => r.Reader.FirstName),
        new("birthdate", "Ngày sinh", r => r.Reader.BirthDate),
        new("sex", "Giới tính", r => r.Reader.Sex switch { Sex.Male => "Nam", Sex.Female => "Nữ", _ => null }),
        new("readertype", "Loại bạn đọc", r => r.ReaderType),
        new("class", "Lớp", r => r.Class),
        new("course", "Khoá", r => r.Course),
        new("email", "Email", r => r.Reader.Email),
        new("phone", "Điện thoại", r => r.Reader.Phone),
        new("address", "Địa chỉ", r => r.Reader.Address),
        new("citizenid", "Số CCCD", r => r.Reader.CitizenId),
        new("carduid", "UID thẻ", r => r.Reader.CardUid),
        new("issuedate", "Ngày cấp thẻ", r => r.Reader.IssueDate),
        new("expiredate", "Ngày hết hạn", r => r.Reader.ExpireDate),
        new("status", "Trạng thái", r => r.Reader.Status == IHasStatus.Active ? "Hoạt động" : "Khoá"),
        new("lockreason", "Lý do khoá", r => r.Reader.LockReason),
    ];

    public static IReadOnlyList<CrudExportField> ExportFields => [.. ExportColumns.Select(c => new CrudExportField(c.Key, c.Header))];

    public async Task<byte[]> ExportAsync(ReaderExportRequest request, CancellationToken ct)
    {
        var columns = CrudExcel.Select(ExportColumns, request.Fields?.ToList());
        var query = Query(request.Search ?? new ReaderSearch());
        if (await query.CountAsync(ct) > CrudExcel.MaxExportRows)
            throw new BusinessRuleException("EXPORT_TOO_MANY_ROWS", $"Mỗi lần xuất tối đa {CrudExcel.MaxExportRows} bạn đọc — lọc thêm theo lớp, khoá… rồi xuất từng phần.");
        var readers = await query.ToListAsync(ct);
        var types = await NamesAsync<ReaderType>(ct);
        var classes = await NamesAsync<SchoolClass>(ct);
        var courses = await NamesAsync<Course>(ct);
        return CrudExcel.Export("Bạn đọc", columns, readers.Select(r => new ReaderExportRow(r,
            Name(types, r.ReaderTypeId), Name(classes, r.ClassId), Name(courses, r.CourseId))));
    }

    private sealed record ReaderExportRow(Reader Reader, string? ReaderType, string? Class, string? Course);

    private async Task<Dictionary<long, string>> NamesAsync<T>(CancellationToken ct) where T : NamedCatalogItem =>
        await Db.Set<T>().AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);

    private static string? Name(Dictionary<long, string> names, long? id) => id is { } v ? names.GetValueOrDefault(v) : null;

    // ── Nhập Excel (monolith: Reader/Import — bố cục cột cố định; ghép cột tuỳ chọn để sau) ──

    public IReadOnlyList<CrudImportColumn> ImportColumns =>
    [
        new("cardno", "Số thẻ", Required: true, Note: "Chữ, số, '.', '-', '_'; không trùng.", "Cardno", "Ma the", "So the ban doc"),
        new("lastname", "Họ đệm", false, "Bỏ trống nếu dùng cột Họ và tên.", "LastName", "Ho", "Ho dem"),
        new("firstname", "Tên", false, "Bắt buộc nếu không có cột Họ và tên.", "FirstName"),
        new("fullname", "Họ và tên", false, "Tách chữ cuối làm Tên.", "Ho ten", "FullName"),
        new("birthdate", "Ngày sinh", false, "dd/MM/yyyy", "BirthDate"),
        new("sex", "Giới tính", false, "Nam / Nữ", "Gioi tinh", "Sex"),
        new("readertype", "Loại bạn đọc", false, "Tên đúng như danh mục Loại bạn đọc.", "ReaderType", "Doi tuong"),
        new("class", "Lớp", false, "Tên đúng như danh mục Lớp.", "Class"),
        new("course", "Khoá", false, "Tên đúng như danh mục Khoá.", "Course", "Nien khoa"),
        new("email", "Email"),
        new("phone", "Điện thoại", false, null, "Phone", "So dien thoai"),
        new("address", "Địa chỉ", false, null, "Address"),
        new("citizenid", "Số CCCD", false, null, "CitizenId", "CCCD"),
        new("carduid", "UID thẻ", false, "Mã chip/RFID của thẻ; không trùng.", "CardUid", "UID"),
        new("issuedate", "Ngày cấp thẻ", false, "Bỏ trống = hôm nay.", "IssueDate", "Ngay cap"),
        new("expiredate", "Ngày hết hạn", false, "Bỏ trống = 1 năm sau ngày cấp.", "ExpireDate", "Han the"),
    ];

    public ReaderRequest MapImportRow(CrudImportRow row)
    {
        var (lastName, firstName) = (row.Get("lastname"), row.Get("firstname"));
        if (firstName is null && row.Get("fullname") is { } full)
        {
            var parts = full.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            (lastName, firstName) = (parts.Length > 1 ? string.Join(' ', parts[..^1]) : null, parts[^1]);
        }
        var issue = row.Date("issuedate", "Ngày cấp thẻ") ?? Today();
        return new ReaderRequest(
            row.Required("cardno", "Số thẻ"), lastName,
            firstName ?? throw new BusinessRuleException("IMPORT_VALUE_REQUIRED", "Thiếu Tên (hoặc Họ và tên)."),
            CitizenId: row.Get("citizenid"), CardUid: row.Get("carduid"), Email: row.Get("email"), Phone: row.Get("phone"), Address: row.Get("address"),
            BirthDate: row.Date("birthdate", "Ngày sinh"), Sex: ParseSex(row.Get("sex")),
            IssueDate: issue, ExpireDate: row.Date("expiredate", "Ngày hết hạn") ?? issue.AddYears(1),
            ReaderTypeName: row.Get("readertype"), ClassName: row.Get("class"), CourseName: row.Get("course"));
    }

    private static int? ParseSex(string? text) => text?.Trim().ToUpperInvariant() switch
    {
        null or "" => null,
        "NAM" or "1" or "M" or "MALE" => Sex.Male,
        "NỮ" or "NU" or "0" or "F" or "FEMALE" => Sex.Female,
        _ => throw new BusinessRuleException("IMPORT_VALUE_INVALID", $"Giới tính '{text}' không hợp lệ (Nam/Nữ)."),
    };

    private async Task<ReaderDto> ChangeAsync(Guid publicId, Action<Reader> change, CancellationToken ct)
    {
        var reader = await LoadAsync(publicId, ct);
        change(reader);
        await PublishAsync(reader, deleted: false, ct);
        await AuditAsync(reader, CrudChange.StatusChanged, ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(reader);
    }

    private Task PublishAsync(Reader reader, bool deleted, CancellationToken ct)
    {
        if (reader.PublicId == Guid.Empty) reader.PublicId = Guid.CreateVersion7(); // bạn đọc mới: interceptor chỉ gán khi còn trống
        return publisher.Publish(new ReaderChanged
        {
            TenantId = tenant.RequireTenantId(),
            Actor = new EventActor(actor.Id, actor.Kind),
            ReaderPublicId = reader.PublicId,
            CardNo = reader.CardNo,
            FullName = reader.FullName,
            ReaderTypeId = reader.ReaderTypeId,
            ClassId = reader.ClassId,
            CourseId = reader.CourseId,
            Status = reader.Status,
            ExpireDate = reader.ExpireDate,
            Deleted = deleted,
            Version = reader.Version + (deleted ? 1 : 0),
        }, ct);
    }

    private async Task EnsureExistsAsync<T>(long? id, string label, CancellationToken ct) where T : NamedCatalogItem
    {
        if (id is { } value && !await Db.Set<T>().AnyAsync(x => x.Id == value, ct))
            throw new BusinessRuleException("READER_REF_NOT_FOUND", $"{label} đã chọn không còn trong danh mục.");
    }

    private async Task<long?> IdByNameAsync<T>(string? name, string label, CancellationToken ct) where T : NamedCatalogItem
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var key = name.Trim();
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        var id = await Db.Set<T>().Where(x => x.Name.ToLower() == key.ToLower()).Select(x => (long?)x.Id).FirstOrDefaultAsync(ct);
#pragma warning restore CA1862, CA1304, CA1311
        return id ?? throw new BusinessRuleException("IMPORT_REF_NOT_FOUND", $"{label} '{key}' chưa có trong danh mục — thêm ở Tham số bạn đọc trước.");
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(7)); // giờ Việt Nam

    private static ReaderData ToData(ReaderRequest r) => new(
        r.CardNo, r.LastName, r.FirstName, r.CitizenId, r.CardUid, r.Email, r.Phone, r.Address, r.BirthDate, r.Sex,
        r.ReaderTypeId, r.ClassId, r.CourseId, r.OrgId, r.DegreeId, r.EthnicityId, r.AcademicTitleId, r.IssueDate, r.ExpireDate);
}
