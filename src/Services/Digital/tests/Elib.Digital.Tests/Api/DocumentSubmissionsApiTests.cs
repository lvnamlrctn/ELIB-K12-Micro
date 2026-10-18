using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.Contracts.Events.Platform;
using Elib.Digital.Application;

namespace Elib.Digital.Tests.Api;

public sealed class DocumentSubmissionsApiTests(DigitalApiFactory factory) : DigitalTestBase(factory)
{
    private static readonly ReaderSubmitRequest FileFields = new(
        "Luận văn tốt nghiệp", "Nguyễn Văn A", null, "Tóm tắt", Guid.CreateVersion7(), "luan-van.pdf", "pdf", 1_000_000);

    [Fact]
    public async Task Reader_requests_an_upload_ticket_then_submits_and_sees_it_pending()
    {
        var tenantId = NewTenant();
        var readerId = await RegisterReaderAsync(tenantId, "SUB-001");
        var reader = ReaderClient(tenantId, "SUB-001", readerId);

        var ticket = await Read<MediaUploadTicket>(await reader.PostAsJsonAsync(
            U("/api/reader/document-submissions/upload-ticket"), new SubmissionUploadRequest("luan-van.pdf", "application/pdf", 1_000_000), Json));
        Assert.Contains("document-submission", Factory.Uploads.RequestedPurposes);
        Assert.NotEqual(Guid.Empty, ticket.FileId);

        var submitted = await Read<DocumentSubmissionDto>(await reader.PostAsJsonAsync(
            U("/api/reader/document-submissions"), FileFields with { MediaFileId = ticket.FileId }, Json));
        Assert.Equal((FileFields.Title, 1), (submitted.Title, submitted.Status)); // 1 = Chờ duyệt
        Assert.Equal(ticket.FileId, submitted.MediaFileId);
        Assert.Contains(submitted.MediaFileId, Factory.Uploads.Completed);
        Assert.Null(submitted.CardNo); // ForReader() ẩn thông tin nội bộ

        var myList = await Read<List<DocumentSubmissionDto>>(await reader.GetAsync(U("/api/reader/document-submissions")));
        Assert.Equal(submitted.PublicId, Assert.Single(myList).PublicId);
    }

    [Fact]
    public async Task Staff_approves_a_submission_and_it_becomes_a_hidden_ebook_item_with_its_file()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var readerId = await RegisterReaderAsync(tenantId, "SUB-002");
        var reader = ReaderClient(tenantId, "SUB-002", readerId);
        var submitted = await Read<DocumentSubmissionDto>(await reader.PostAsJsonAsync(U("/api/reader/document-submissions"), FileFields, Json));

        var pending = await Read<CrudPage<DocumentSubmissionDto>>(await staff.PostAsJsonAsync(U("/api/document-submissions/Search"), new DocumentSubmissionSearch(), Json));
        var found = Assert.Single(pending.Items);
        Assert.Equal("SUB-002", found.CardNo); // thủ thư thấy đủ thông tin, khác bạn đọc

        var approved = await Read<DocumentSubmissionDto>(await staff.PostAsync(U($"/api/document-submissions/{submitted.PublicId}/Approve"), null));
        Assert.Equal(2, approved.Status); // 2 = Đã duyệt
        Assert.NotNull(approved.CreatedItemPublicId);

        var item = await Read<EbookItemDto>(await staff.GetAsync(U($"/api/items/GetById/{approved.CreatedItemPublicId}")));
        Assert.Equal((FileFields.Title, FileFields.Author, 1), (item.Title, item.Author, item.Status)); // 1 = Ẩn (chưa tự xuất bản)

        var files = await Read<CrudPage<EbookFileDto>>(await staff.PostAsJsonAsync(U("/api/files/Search"), new EbookFileSearch { EbookItemPublicId = item.PublicId }, Json));
        Assert.Equal(FileFields.MediaFileId, Assert.Single(files.Items).MediaFileId);

        Assert.Contains(Factory.PublishedOf<NotificationRequested>(), n => n.TemplateCode == EbookTemplates.SubmissionApproved);

        // Gọi duyệt lại (mạng chập chờn) → không tạo thêm tài liệu số.
        var again = await Read<DocumentSubmissionDto>(await staff.PostAsync(U($"/api/document-submissions/{submitted.PublicId}/Approve"), null));
        Assert.Equal(approved.CreatedItemPublicId, again.CreatedItemPublicId);
        Assert.Equal(1, (await Read<CrudPage<EbookItemDto>>(await staff.PostAsJsonAsync(U("/api/items/Search"), new EbookItemSearch(), Json))).TotalCount);
    }

    [Fact]
    public async Task Staff_rejects_a_submission_with_a_reason_and_the_reader_sees_it()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var readerId = await RegisterReaderAsync(tenantId, "SUB-003");
        var reader = ReaderClient(tenantId, "SUB-003", readerId);
        var submitted = await Read<DocumentSubmissionDto>(await reader.PostAsJsonAsync(U("/api/reader/document-submissions"), FileFields, Json));

        Assert.Equal("REASON_REQUIRED", (await Error(await staff.PostAsJsonAsync(
            U("/api/document-submissions/Reject"), new RejectSubmissionRequest(submitted.PublicId, ""), Json))).Code);

        var rejected = await Read<DocumentSubmissionDto>(await staff.PostAsJsonAsync(
            U("/api/document-submissions/Reject"), new RejectSubmissionRequest(submitted.PublicId, "Thiếu trang bìa"), Json));
        Assert.Equal((3, "Thiếu trang bìa"), (rejected.Status, rejected.RejectReason)); // 3 = Từ chối
        Assert.Contains(Factory.PublishedOf<NotificationRequested>(), n => n.TemplateCode == EbookTemplates.SubmissionRejected);

        var myList = await Read<List<DocumentSubmissionDto>>(await reader.GetAsync(U("/api/reader/document-submissions")));
        var mine = Assert.Single(myList);
        Assert.Equal((3, "Thiếu trang bìa"), (mine.Status, mine.RejectReason));
        Assert.Null(mine.ReviewedBy); // ForReader() ẩn ai duyệt/từ chối
    }

    [Fact]
    public async Task Another_tenants_reader_does_not_see_submissions_of_a_different_tenant()
    {
        var tenantA = NewTenant();
        var readerA = ReaderClient(tenantA, "SUB-A", await RegisterReaderAsync(tenantA, "SUB-A"));
        await readerA.PostAsJsonAsync(U("/api/reader/document-submissions"), FileFields, Json);

        var tenantB = NewTenant();
        var readerB = ReaderClient(tenantB, "SUB-B", await RegisterReaderAsync(tenantB, "SUB-B"));
        Assert.Empty(await Read<List<DocumentSubmissionDto>>(await readerB.GetAsync(U("/api/reader/document-submissions"))));

        Assert.Equal(0, (await Read<CrudPage<DocumentSubmissionDto>>(
            await Staff(tenantB).PostAsJsonAsync(U("/api/document-submissions/Search"), new DocumentSubmissionSearch(), Json))).TotalCount);
    }

    private async Task<Guid> RegisterReaderAsync(long tenantId, string card)
    {
        var reader = Reader(tenantId, card, email: "reader@example.com");
        await Publish(reader);
        return reader.ReaderPublicId;
    }
}
