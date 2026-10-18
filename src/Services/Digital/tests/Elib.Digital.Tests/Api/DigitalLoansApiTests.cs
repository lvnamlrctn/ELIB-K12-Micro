using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Platform;
using Elib.Digital.Api;
using Elib.Digital.Application;
using Elib.Digital.Domain;
using Elib.Digital.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Digital.Tests.Api;

public sealed class DigitalLoansApiTests(DigitalApiFactory factory) : DigitalTestBase(factory)
{
    private async Task<(long TenantId, HttpClient Staff, EbookCollectionDto Collection)> SetupAsync()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var collection = await Read<EbookCollectionDto>(await staff.PostAsJsonAsync(U("/api/collections/Add"), new EbookCollectionRequest("Văn học"), Json));
        return (tenantId, staff, collection);
    }

    private static async Task<EbookItemDto> AddItemAsync(HttpClient staff, long collectionId, string title, int? printCopies = null, int? offlineDays = null) =>
        await Read<EbookItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"),
            new EbookItemFields(title, CollectionId: collectionId, PrintCopies: printCopies, OfflineDays: offlineDays), Json));

    private async Task<Guid> RegisterReaderAsync(long tenantId, string card, long? typeId = null, string? email = "reader@example.com")
    {
        var reader = Reader(tenantId, card, typeId: typeId, email: email);
        await Publish(reader);
        return reader.ReaderPublicId;
    }

    [Fact]
    public async Task Borrowing_resumes_an_open_loan_and_reports_expiry_when_offline_days_pass()
    {
        var (tenantId, staff, collection) = await SetupAsync();
        var item = await AddItemAsync(staff, collection.Id, "Tắt đèn", offlineDays: 2);
        var readerId = await RegisterReaderAsync(tenantId, "DG-001");
        var reader = ReaderClient(tenantId, "DG-001", readerId);

        var loan = await Read<EbookLoanDto>(await reader.PostAsJsonAsync(U("/api/reader/loans/Borrow"), new ReaderBorrowRequest(item.PublicId), Json));
        Assert.Equal(1, loan.Status);

        var resumed = await Read<EbookLoanDto>(await reader.PostAsJsonAsync(U("/api/reader/loans/Borrow"), new ReaderBorrowRequest(item.PublicId), Json));
        Assert.Equal(loan.PublicId, resumed.PublicId);

        Factory.Clock.Advance(TimeSpan.FromDays(3));
        Assert.Equal("EBOOK_LOAN_EXPIRED", (await Error(await reader.PostAsJsonAsync(U("/api/reader/loans/Borrow"), new ReaderBorrowRequest(item.PublicId), Json))).Code);
    }

    [Fact]
    public async Task Policy_can_deny_reading_a_collection_for_a_reader_type()
    {
        var (tenantId, staff, collection) = await SetupAsync();
        var item = await AddItemAsync(staff, collection.Id, "Cấm đọc");
        await staff.PostAsJsonAsync(U("/api/policies-by-collection/Add"), new PolicyDigitalByCollectionRequest(1, collection.Id, Read: false), Json);
        var readerId = await RegisterReaderAsync(tenantId, "DG-002", typeId: 1);
        var reader = ReaderClient(tenantId, "DG-002", readerId);

        Assert.Equal("EBOOK_READ_DENIED", (await Error(await reader.PostAsJsonAsync(U("/api/reader/loans/Borrow"), new ReaderBorrowRequest(item.PublicId), Json))).Code);
    }

    [Fact]
    public async Task Print_copies_cap_concurrent_loans_and_queue_overflow_into_a_reservation()
    {
        var (tenantId, staff, collection) = await SetupAsync();
        var item = await AddItemAsync(staff, collection.Id, "Một bản", printCopies: 1);
        var readerA = ReaderClient(tenantId, "DG-A", await RegisterReaderAsync(tenantId, "DG-A"));
        var readerB = ReaderClient(tenantId, "DG-B", await RegisterReaderAsync(tenantId, "DG-B"));

        await Read<EbookLoanDto>(await readerA.PostAsJsonAsync(U("/api/reader/loans/Borrow"), new ReaderBorrowRequest(item.PublicId), Json));
        Assert.Equal("EBOOK_AT_CAPACITY", (await Error(await readerB.PostAsJsonAsync(U("/api/reader/loans/Borrow"), new ReaderBorrowRequest(item.PublicId), Json))).Code);

        // Còn chỗ (chưa ai mượn) nên không được đặt trước — phải mượn thẳng.
        var readerC = ReaderClient(tenantId, "DG-C", await RegisterReaderAsync(tenantId, "DG-C", typeId: 1));
        Assert.Equal("EBOOK_AT_CAPACITY", (await Error(await readerC.PostAsJsonAsync(U("/api/reader/loans/Borrow"), new ReaderBorrowRequest(item.PublicId), Json))).Code);
        Assert.Equal("EBOOK_RESERVATION_NOT_ALLOWED",
            (await Error(await readerC.PostAsJsonAsync(U("/api/reader/reservations"), new ReaderReserveRequest(item.PublicId), Json))).Code);

        // Cấp quyền đặt trước cho loại bạn đọc rồi thử lại.
        await staff.PostAsJsonAsync(U("/api/policies/Add"), new PolicyDigitalRequest(ReservationExpireHours: 24), Json);
        var reservation = await Read<EbookReservationDto>(await readerC.PostAsJsonAsync(U("/api/reader/reservations"), new ReaderReserveRequest(item.PublicId), Json));
        Assert.Equal(1, reservation.Status); // Pending
        Assert.Equal("EBOOK_RESERVATION_EXISTS", (await Error(await readerC.PostAsJsonAsync(U("/api/reader/reservations"), new ReaderReserveRequest(item.PublicId), Json))).Code);

        // Trả sách — đẩy hàng đợi: reservation của C lên Ready.
        var loanA = Assert.Single(await Read<List<EbookLoanDto>>(await readerA.GetAsync(U("/api/reader/loans"))));
        await readerA.PostAsJsonAsync(U($"/api/reader/loans/{loanA.PublicId}/Return"), (object?)null, Json);

        var readyList = await Read<List<EbookReservationDto>>(await readerC.GetAsync(U("/api/reader/reservations")));
        Assert.Equal(2, Assert.Single(readyList).Status); // Ready
        Assert.Contains(Factory.PublishedOf<NotificationRequested>(), n => n.TemplateCode == EbookTemplates.HoldReady);

        // C giờ mượn thẳng được, dùng suất đã giữ — không bị chặn bởi sức chứa.
        var loanC = await Read<EbookLoanDto>(await readerC.PostAsJsonAsync(U("/api/reader/loans/Borrow"), new ReaderBorrowRequest(item.PublicId), Json));
        Assert.Equal(1, loanC.Status);
    }

    [Fact]
    public async Task Max_document_policy_blocks_borrowing_beyond_the_configured_limit()
    {
        var (tenantId, staff, collection) = await SetupAsync();
        var first = await AddItemAsync(staff, collection.Id, "Sách 1");
        var second = await AddItemAsync(staff, collection.Id, "Sách 2");
        await staff.PostAsJsonAsync(U("/api/policies/Add"), new PolicyDigitalRequest(MaxDocument: 1), Json);
        var readerId = await RegisterReaderAsync(tenantId, "DG-003", typeId: 1);
        var reader = ReaderClient(tenantId, "DG-003", readerId);

        await Read<EbookLoanDto>(await reader.PostAsJsonAsync(U("/api/reader/loans/Borrow"), new ReaderBorrowRequest(first.PublicId), Json));
        Assert.Equal("EBOOK_MAX_DOCUMENTS", (await Error(await reader.PostAsJsonAsync(U("/api/reader/loans/Borrow"), new ReaderBorrowRequest(second.PublicId), Json))).Code);
    }

    [Fact]
    public async Task Staff_can_recall_a_loan_with_a_reason_and_promote_the_queue()
    {
        var (tenantId, staff, collection) = await SetupAsync();
        var item = await AddItemAsync(staff, collection.Id, "Thu hồi", printCopies: 1);
        await staff.PostAsJsonAsync(U("/api/policies/Add"), new PolicyDigitalRequest(ReservationExpireHours: 24), Json);
        var readerA = ReaderClient(tenantId, "DG-R1", await RegisterReaderAsync(tenantId, "DG-R1"));
        var readerB = ReaderClient(tenantId, "DG-R2", await RegisterReaderAsync(tenantId, "DG-R2", typeId: 1));

        var loan = await Read<EbookLoanDto>(await readerA.PostAsJsonAsync(U("/api/reader/loans/Borrow"), new ReaderBorrowRequest(item.PublicId), Json));
        await Read<EbookReservationDto>(await readerB.PostAsJsonAsync(U("/api/reader/reservations"), new ReaderReserveRequest(item.PublicId), Json));

        Assert.Equal("REASON_REQUIRED", (await Error(await staff.PostAsJsonAsync(U("/api/loans/Recall"), new RecallLoanRequest(loan.PublicId, ""), Json))).Code);
        var recalled = await Read<EbookLoanDto>(await staff.PostAsJsonAsync(U("/api/loans/Recall"), new RecallLoanRequest(loan.PublicId, "Hết hạn license nhà xuất bản"), Json));
        Assert.Equal(2, recalled.Status); // Recalled

        var readyList = await Read<List<EbookReservationDto>>(await readerB.GetAsync(U("/api/reader/reservations")));
        Assert.Equal(2, Assert.Single(readyList).Status); // Ready — thu hồi cũng đẩy hàng đợi
    }

    [Fact]
    public async Task Reader_endpoints_404_for_another_readers_loan_or_reservation()
    {
        var (tenantId, staff, collection) = await SetupAsync();
        var item = await AddItemAsync(staff, collection.Id, "Riêng tư");
        var owner = ReaderClient(tenantId, "DG-OWN", await RegisterReaderAsync(tenantId, "DG-OWN"));
        var other = ReaderClient(tenantId, "DG-OTHER", await RegisterReaderAsync(tenantId, "DG-OTHER"));

        var loan = await Read<EbookLoanDto>(await owner.PostAsJsonAsync(U("/api/reader/loans/Borrow"), new ReaderBorrowRequest(item.PublicId), Json));
        Assert.Equal(404, (int)(await other.PostAsJsonAsync(U($"/api/reader/loans/{loan.PublicId}/Return"), (object?)null, Json)).StatusCode);
    }

    [Fact]
    public async Task File_url_gates_on_free_flag_login_and_an_active_loan_download_also_needs_allow_download()
    {
        var (tenantId, staff, collection) = await SetupAsync();
        var freeItem = await Read<EbookItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"),
            new EbookItemFields("Công khai", CollectionId: collection.Id, Free: true), Json));
        var gatedItem = await AddItemAsync(staff, collection.Id, "Giới hạn");
        var freeFile = await Read<EbookFileDto>(await staff.PostAsJsonAsync(U("/api/files/Add"), new EbookFileRequest(freeItem.PublicId, Guid.CreateVersion7()), Json));
        var gatedFile = await Read<EbookFileDto>(await staff.PostAsJsonAsync(U("/api/files/Add"), new EbookFileRequest(gatedItem.PublicId, Guid.CreateVersion7()), Json));

        var anon = Opac(tenantId);
        await Read<object>(await anon.GetAsync(U($"/api/opac/items/{freeItem.PublicId}/files/{freeFile.PublicId}/url")));

        Assert.Equal(401, (int)(await anon.GetAsync(U($"/api/opac/items/{gatedItem.PublicId}/files/{gatedFile.PublicId}/url"))).StatusCode);

        var readerId = await RegisterReaderAsync(tenantId, "DG-004");
        var reader = ReaderClient(tenantId, "DG-004", readerId);
        Assert.Equal(403, (int)(await reader.GetAsync(U($"/api/opac/items/{gatedItem.PublicId}/files/{gatedFile.PublicId}/url"))).StatusCode);

        await reader.PostAsJsonAsync(U("/api/reader/loans/Borrow"), new ReaderBorrowRequest(gatedItem.PublicId), Json);
        await Read<object>(await reader.GetAsync(U($"/api/opac/items/{gatedItem.PublicId}/files/{gatedFile.PublicId}/url")));
        Assert.Equal("EBOOK_DOWNLOAD_NOT_ALLOWED",
            (await Error(await reader.GetAsync(U($"/api/opac/items/{gatedItem.PublicId}/files/{gatedFile.PublicId}/url?download=true")))).Code);
    }

    [Fact]
    public async Task Expiry_job_auto_expires_overdue_loans_and_promotes_the_queue()
    {
        var (tenantId, staff, collection) = await SetupAsync();
        var item = await AddItemAsync(staff, collection.Id, "Hết hạn tự động", printCopies: 1, offlineDays: 1);
        await staff.PostAsJsonAsync(U("/api/policies/Add"), new PolicyDigitalRequest(ReservationExpireHours: 24), Json);
        var readerA = ReaderClient(tenantId, "DG-J1", await RegisterReaderAsync(tenantId, "DG-J1"));
        var readerB = ReaderClient(tenantId, "DG-J2", await RegisterReaderAsync(tenantId, "DG-J2", typeId: 1));

        await readerA.PostAsJsonAsync(U("/api/reader/loans/Borrow"), new ReaderBorrowRequest(item.PublicId), Json);
        await Read<EbookReservationDto>(await readerB.PostAsJsonAsync(U("/api/reader/reservations"), new ReaderReserveRequest(item.PublicId), Json));
        Factory.Clock.Advance(TimeSpan.FromDays(2));

        using var scope = Factory.Services.CreateScope();
        using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
        var job = scope.ServiceProvider.GetRequiredService<EbookLoanExpiryJob>();
        var (expired, promoted) = await job.RunForTenantAsync(scope.ServiceProvider, CancellationToken.None);
        Assert.Equal((1, 1), (expired, promoted));
        Assert.Contains(Factory.PublishedOf<NotificationRequested>(), n => n.TemplateCode == EbookTemplates.LoanExpired);

        var readyList = await Read<List<EbookReservationDto>>(await readerB.GetAsync(U("/api/reader/reservations")));
        Assert.Equal(2, Assert.Single(readyList).Status); // Ready
    }
}
