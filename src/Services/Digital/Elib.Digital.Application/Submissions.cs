using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elib.Digital.Application;

/// <summary>Vé upload ký sẵn — giống UploadTicketDto của media nhưng tránh phụ thuộc thẳng vào project của media
/// (digital gọi qua HTTP nội bộ, xem <see cref="IMediaUploadTicket"/>).</summary>
public sealed record MediaUploadTicket(Guid FileId, string UploadUrl, DateTimeOffset ExpiresAt, long MaxBytes);

/// <summary>
/// Xin vé upload/hoàn tất hộ bạn đọc đã tự xác thực (service token tới media's endpoint nội bộ — quyết định #3 của plan giai
/// đoạn 2). Digital không nhận trực tiếp nội dung file; chỉ chuyển tiếp yêu cầu rồi để trình duyệt PUT thẳng vào media.
/// </summary>
public interface IMediaUploadTicket
{
    Task<MediaUploadTicket> RequestAsync(string purpose, string fileName, string contentType, long size, CancellationToken ct);

    Task CompleteAsync(Guid fileId, CancellationToken ct);
}

public sealed class DocumentSubmissionSearch : CrudSearch
{
    public Guid? ReaderPublicId { get; set; }
    public int? SubmissionStatus { get; set; }
}

public sealed record DocumentSubmissionDto(
    long Id, Guid PublicId, Guid ReaderPublicId, string? CardNo, string? ReaderName, string Title, string? Author, long? DigTypeId,
    string? DigTypeCode, string? Abstract, Guid MediaFileId, string FileName, string? FileExt, long FileSize, int Status, string? RejectReason,
    long? ReviewedBy, DateTimeOffset? ReviewedAt, Guid? CreatedItemPublicId, DateTimeOffset CreatedAt)
{
    /// <summary>Bạn đọc tự xem (OPAC) — không lộ ai duyệt.</summary>
    public DocumentSubmissionDto ForReader() => this with { CardNo = null, ReaderName = null, ReviewedBy = null };
}

/// <summary>Request của resource — không dùng (nộp/duyệt/từ chối đều qua phương thức nghiệp vụ riêng), chỉ để khớp CrudResource.</summary>
public sealed record DocumentSubmissionOpUnused;

public sealed record SubmissionUploadRequest(string FileName, string ContentType, long Size);
public sealed record ReaderSubmitRequest(string Title, string? Author, long? DigTypeId, string? Abstract, Guid MediaFileId, string FileName, string? FileExt, long FileSize);
public sealed record RejectSubmissionRequest(Guid SubmissionPublicId, string Reason);

/// <summary>
/// Nộp tài liệu bạn đọc (LRC: DocumentSubmission — port thêm luồng duyệt/từ chối, xem quyết định #5 của plan giai đoạn 2). Không
/// có Add/Update chung — bạn đọc nộp qua <see cref="SubmitAsync"/>, thủ thư chỉ tra cứu + Duyệt/Từ chối, đúng khuôn
/// <see cref="EbookLoanResource"/>.
/// </summary>
public sealed class DocumentSubmissionResource(
    ICrudDbContext db, TimeProvider clock, IMediaUploadTicket media, EbookReaderNotifier notifier, ITenantContext tenant, IOptions<DigitalOptions> options)
    : CrudResource<DocumentSubmissionResource, DocumentSubmission, DocumentSubmissionSearch, DocumentSubmissionOpUnused, DocumentSubmissionDto>(db)
{
    public const string Purpose = "document-submission";

    private readonly DigitalOptions _options = options.Value;

    protected override string EntityName => "Tài liệu bạn đọc nộp";

    protected override string? Describe(DocumentSubmission entity) => entity.Title;

    protected override Expression<Func<DocumentSubmission, DocumentSubmissionDto>> Projection => x => new DocumentSubmissionDto(
        x.Id, x.PublicId, x.ReaderPublicId,
        Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == x.ReaderPublicId).Select(r => r.CardNo).FirstOrDefault(),
        Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == x.ReaderPublicId).Select(r => r.FullName).FirstOrDefault(),
        x.Title, x.Author, x.DigTypeId,
        Db.Set<EbookDigType>().Where(t => t.Id == x.DigTypeId).Select(t => t.Code).FirstOrDefault(),
        x.Abstract, x.MediaFileId, x.FileName, x.FileExt, x.FileSize, x.Status, x.RejectReason, x.ReviewedBy, x.ReviewedAt,
        x.CreatedItemPublicId, x.CreatedAt);

    protected override DocumentSubmission Create(DocumentSubmissionOpUnused request) => throw new NotSupportedException("Nộp tài liệu qua SubmitAsync.");

    protected override void Update(DocumentSubmission entity, DocumentSubmissionOpUnused request) =>
        throw new NotSupportedException("Không sửa trực tiếp — Duyệt/Từ chối qua ApproveAsync/RejectAsync.");

    protected override IQueryable<DocumentSubmission> Filter(IQueryable<DocumentSubmission> query, DocumentSubmissionSearch search)
    {
        if (search.ReaderPublicId is { } reader) query = query.Where(x => x.ReaderPublicId == reader);
        if (search.SubmissionStatus is { } status) query = query.Where(x => x.Status == status);
        return query;
    }

    protected override IOrderedQueryable<DocumentSubmission> Order(IQueryable<DocumentSubmission> query) =>
        query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);

    public Task<MediaUploadTicket> RequestUploadAsync(SubmissionUploadRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return media.RequestAsync(Purpose, request.FileName, request.ContentType, request.Size, ct);
    }

    public async Task<DocumentSubmissionDto> SubmitAsync(Guid readerPublicId, ReaderSubmitRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        await media.CompleteAsync(request.MediaFileId, ct);
        var submission = DocumentSubmission.Submit(
            readerPublicId, request.Title, request.Author, request.DigTypeId, request.Abstract, request.MediaFileId, request.FileName,
            request.FileExt, request.FileSize);
        Set.Add(submission);
        await Db.SaveChangesAsync(ct);
        return (await ToDtosAsync([submission], ct))[0];
    }

    /// <summary>Tài liệu bạn đọc đã nộp (OPAC "Tài liệu đã nộp") — mọi trạng thái, mới nhất trước.</summary>
    public async Task<IReadOnlyList<DocumentSubmissionDto>> MyListAsync(Guid readerPublicId, CancellationToken ct) =>
        await Set.AsNoTracking().Where(x => x.ReaderPublicId == readerPublicId).OrderByDescending(x => x.CreatedAt).Select(Projection).ToListAsync(ct);

    /// <summary>Duyệt — tạo EbookItem (Ẩn, thủ thư tự Xuất bản sau khi xem lại) + EbookFile từ nội dung đã nộp, trong 1 transaction
    /// (khuôn BibResource.AddAsync). Gọi lại khi đã duyệt (mạng chập chờn) → trả kết quả cũ, không tạo trùng.</summary>
    public async Task<DocumentSubmissionDto> ApproveAsync(Guid submissionPublicId, long? actorId, CancellationToken ct)
    {
        var submission = await Set.FirstOrDefaultAsync(x => x.PublicId == submissionPublicId, ct) ?? throw new NotFoundException(EntityName, submissionPublicId);
        if (submission.Status == DocumentSubmissionStatus.Approved) return (await ToDtosAsync([submission], ct))[0];

        var strategy = Db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await Db.Database.BeginTransactionAsync(token);
            var now = clock.GetUtcNow();
            var fields = new EbookItemFields(submission.Title, Author: submission.Author, TypeId: submission.DigTypeId, Status: IHasStatus.Inactive);
            var item = EbookItem.Create(fields, _options.MediaPublicPrefix, tenant.RequireTenantId(), now);
            Db.Set<EbookItem>().Add(item);
            await Db.SaveChangesAsync(token); // cần item.Id (sinh ở DB) trước khi tạo EbookFile

            Db.Set<EbookFile>().Add(EbookFile.Create(item.Id, submission.MediaFileId, new EbookFileFields(FileExt: submission.FileExt, FileSize: submission.FileSize)));
            submission.Approve(actorId, now, item.PublicId);
            var reader = await Db.Set<PatronReplica>().AsNoTracking().FirstOrDefaultAsync(r => r.ReaderPublicId == submission.ReaderPublicId, token);
            // Publish TRƯỚC SaveChanges — NotificationRequested đi qua outbox, chỉ gửi được nếu cùng transaction lưu kèm thay đổi này.
            await notifier.NotifyAsync(reader, EbookTemplates.SubmissionApproved, new Dictionary<string, string> { ["title"] = submission.Title },
                $"ebook-submission-approved:{submission.Id}", token);
            await Db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }, ct);
        return (await ToDtosAsync([submission], ct))[0];
    }

    /// <summary>Từ chối — lý do bắt buộc. Gọi lại khi đã từ chối → trả kết quả cũ.</summary>
    public async Task<DocumentSubmissionDto> RejectAsync(RejectSubmissionRequest request, long? actorId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var submission = await Set.FirstOrDefaultAsync(x => x.PublicId == request.SubmissionPublicId, ct)
            ?? throw new NotFoundException(EntityName, request.SubmissionPublicId);
        if (submission.Status == DocumentSubmissionStatus.Rejected) return (await ToDtosAsync([submission], ct))[0];

        submission.Reject(actorId, clock.GetUtcNow(), request.Reason);
        var reader = await Db.Set<PatronReplica>().AsNoTracking().FirstOrDefaultAsync(r => r.ReaderPublicId == submission.ReaderPublicId, ct);
        await notifier.NotifyAsync(reader, EbookTemplates.SubmissionRejected,
            new Dictionary<string, string> { ["title"] = submission.Title, ["reason"] = request.Reason }, $"ebook-submission-rejected:{submission.Id}", ct);
        await Db.SaveChangesAsync(ct);
        return (await ToDtosAsync([submission], ct))[0];
    }

    private async Task<IReadOnlyList<DocumentSubmissionDto>> ToDtosAsync(List<DocumentSubmission> submissions, CancellationToken ct)
    {
        var ids = submissions.Select(x => x.Id).ToList();
        return await Set.AsNoTracking().Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id).Select(Projection).ToListAsync(ct);
    }
}
