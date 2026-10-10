using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.Circulation.Application;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Circulation;
using Elib.Contracts.Events.Holdings;
using Elib.Contracts.Events.Patron;
using Elib.Contracts.Events.Platform;

namespace Elib.Circulation.Tests.Api;

public sealed class CirculationApiTests : IClassFixture<CirculationApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 9000;
    private static long _nextUserId = 19000;
    private static long _nextMfn = 900;

    private readonly CirculationApiFactory _factory;

    public CirculationApiTests(CirculationApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private long NewTenant(bool licensed = true)
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        if (licensed) _factory.Licenses.Licensed.Add((tenantId, "CIRCULATION"));
        return tenantId;
    }

    private HttpClient Staff(long tenantId, params string[] grants)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        _factory.Permissions.Grants[userId] = grants.Length == 0 ? ["*"] : grants;
        return _factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task<(int Status, string? Code, string? Detail)> Error(HttpResponseMessage response)
    {
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        return ((int)response.StatusCode, root.GetProperty("code").GetString(), root.GetProperty("detail").GetString());
    }

    private static ReaderChanged Reader(long tenantId, string card, long? typeId = null, int status = 2, DateOnly? expire = null, long version = 0) => new()
    {
        TenantId = tenantId, ReaderPublicId = Guid.CreateVersion7(), CardNo = card, FullName = "Bạn đọc " + card, ReaderTypeId = typeId,
        ReaderTypeName = "Học sinh", ClassName = "6A1", Status = status, ExpireDate = expire, Version = version,
    };

    private static ItemChanged Item(long tenantId, string barcode, long mfn, string status = "R", long? storeId = 1, long version = 0) => new()
    {
        TenantId = tenantId, ItemPublicId = Guid.CreateVersion7(), Barcode = barcode, BibPublicId = Guid.CreateVersion7(), Mfn = mfn,
        StoreId = storeId, StoreName = "Kho " + storeId, Status = status, Version = version,
    };

    private static BibChanged Bib(long tenantId, long mfn, string title) => new()
    {
        TenantId = tenantId, BibPublicId = Guid.CreateVersion7(), Mfn = mfn, Title = title, Author = "Tô Hoài", Status = 2,
    };

    /// <summary>Đưa vào bản sao qua event (như patron/holdings/catalog phát) và chờ consumer xử lý xong.</summary>
    private async Task Publish(params IntegrationEvent[] events)
    {
        foreach (var e in events) await _factory.PublishAsAsync(e);
        foreach (var e in events) await _factory.WaitConsumedAsync(e);
    }

    private static async Task<CircPlaceDto> AddPlace(HttpClient staff, string code, params long[] stores) =>
        await Read<CircPlaceDto>(await staff.PostAsJsonAsync(U("/api/circ-places/Add"), new CircPlaceRequest(code, "Quầy " + code, stores), Json));

    private static Task<HttpResponseMessage> Checkout(HttpClient staff, string card, long place, params string[] barcodes) =>
        staff.PostAsJsonAsync(U("/api/loans/Checkout"), new CheckoutRequest(card, barcodes, place), Json);

    [Fact]
    public async Task New_tenant_gets_a_desk_and_a_default_policy()
    {
        var tenantId = NewTenant();
        await _factory.PublishAsync(new TenantProvisioned
        {
            TenantId = tenantId, Code = "L" + tenantId, Name = "Trường " + tenantId, Subdomain = "l" + tenantId, TimeZone = "Asia/Ho_Chi_Minh", Modules = [],
        });
        await CirculationApiFactory.WaitForAsync(() => Task.FromResult(_factory.PublishedOf<TenantSeeded>().FirstOrDefault(s => s.TenantId == tenantId)), "TenantSeeded");

        var staff = Staff(tenantId);
        var place = Assert.Single(await Read<List<CircPlaceDto>>(await staff.PostAsJsonAsync(U("/api/circ-places/SearchAll"), new CrudSearch(), Json)));
        Assert.Equal(("QUAY", "Quầy mượn trả"), (place.Code, place.Name));
        var policy = Assert.Single(await Read<List<LoanPolicyDto>>(await staff.PostAsJsonAsync(U("/api/loan-policies/SearchAll"), new CrudSearch(), Json)));
        Assert.Equal(((long?)null, (long?)null, 14, 7), (policy.ReaderTypeId, policy.CircPlaceId, policy.LoanDays, policy.RenewDays));
    }

    [Fact]
    public async Task Checkout_checks_each_item_and_return_closes_the_loan()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "Q1");
        var mfn = Interlocked.Increment(ref _nextMfn);
        var reader = Reader(tenantId, "hs-01");
        await Publish(reader, Reader(tenantId, "HS-02"), Bib(tenantId, mfn, "Dế mèn phiêu lưu ký"),
            Item(tenantId, "VV0001", mfn), Item(tenantId, "VV0002", mfn, status: "I"), Item(tenantId, "VV0003", mfn, status: "L"));

        var panel = await Read<ReaderPanel>(await staff.PostAsJsonAsync(U("/api/loans/Reader"), new ReaderPanelRequest(" hs-01 ", place.Id), Json));
        Assert.Equal(("HS-01", "Bạn đọc hs-01", "Học sinh", "6A1", true, 14), (panel.CardNo, panel.FullName, panel.ReaderTypeName, panel.ClassName, panel.CanBorrow, panel.LoanDays));

        var result = await Read<CheckoutResult>(await Checkout(staff, "HS-01", place.Id, "vv0001", "VV0002", "VV0003", "KHONGCO", "VV0001"));
        Assert.Equal((1, 3), (result.Succeeded, result.Failed));
        Assert.Equal(["Mượn thành công", "chưa xếp giá", "báo mất", "không tồn tại"],
            result.Lines.Select(l => l.Success ? "Mượn thành công" : l.Message.Contains("chưa xếp giá", StringComparison.Ordinal) ? "chưa xếp giá"
                : l.Message.Contains("báo mất", StringComparison.Ordinal) ? "báo mất" : l.Message.Contains("không tồn tại", StringComparison.Ordinal) ? "không tồn tại" : l.Message));
        var loan = result.Lines[0].Loan!;
        Assert.Equal(("VV0001", "Dế mèn phiêu lưu ký", "HS-01", "Bạn đọc hs-01"), (loan.Barcode, loan.Title, loan.CardNo, loan.ReaderName));
        Assert.Equal(14, (loan.DueAt - loan.LoanedAt).TotalDays);

        var created = _factory.PublishedOf<LoanChanged>().Single(e => e.LoanPublicId == loan.PublicId);
        Assert.Equal((reader.ReaderPublicId, mfn, (DateTimeOffset?)null), (created.ReaderPublicId, created.Mfn, created.ReturnedAt));

        // Bạn đọc khác không mượn được bản đang có người mượn.
        var taken = await Read<CheckoutResult>(await Checkout(staff, "HS-02", place.Id, "VV0001"));
        Assert.Equal("Tài liệu đang được mượn.", Assert.Single(taken.Lines).Message);

        var panelAfter = await Read<ReaderPanel>(await staff.PostAsJsonAsync(U("/api/loans/Reader"), new ReaderPanelRequest("HS-01"), Json));
        Assert.Equal(loan.PublicId, Assert.Single(panelAfter.CurrentLoans).PublicId);

        var returned = await Read<ReturnResult>(await staff.PostAsJsonAsync(U("/api/loans/Return"), new ReturnRequest(" vv0001 ", CircPlaceId: place.Id), Json));
        Assert.Equal((loan.PublicId, 0), (returned.Loan.PublicId, returned.OverdueDays));
        Assert.NotNull(returned.Loan.ReturnedAt);
        Assert.NotNull(_factory.PublishedOf<LoanChanged>().Single(e => e.LoanPublicId == loan.PublicId && e.Version == 1).ReturnedAt);

        var again = await staff.PostAsJsonAsync(U("/api/loans/Return"), new ReturnRequest("VV0001"), Json);
        Assert.Equal((404, "LOAN_NOT_FOUND"), ((await Error(again)).Status, (await Error(again)).Code));
        Assert.Equal(1, (await Read<CheckoutResult>(await Checkout(staff, "HS-02", place.Id, "VV0001"))).Succeeded);
    }

    [Fact]
    public async Task Reader_rules_block_locked_expired_and_overdue_readers()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "Q2");
        var mfn = Interlocked.Increment(ref _nextMfn);
        var today = DateOnly.FromDateTime(_factory.Clock.GetUtcNow().UtcDateTime.AddHours(7));
        await Publish(Reader(tenantId, "KHOA", status: 1), Reader(tenantId, "HETHAN", expire: today.AddDays(-1)), Reader(tenantId, "QH"),
            Item(tenantId, "QH01", mfn), Item(tenantId, "QH02", mfn));

        Assert.Equal("READER_CANNOT_BORROW", (await Error(await Checkout(staff, "KHOA", place.Id, "QH01"))).Code);
        Assert.Contains("hết hạn", (await Error(await Checkout(staff, "HETHAN", place.Id, "QH01"))).Detail, StringComparison.Ordinal);
        Assert.Equal(404, (await Error(await Checkout(staff, "KHONGCO", place.Id, "QH01"))).Status);

        Assert.Equal(1, (await Read<CheckoutResult>(await Checkout(staff, "QH", place.Id, "QH01"))).Succeeded);
        _factory.Clock.Advance(TimeSpan.FromDays(20)); // hạn 14 ngày → quá hạn 6 ngày
        var panel = await Read<ReaderPanel>(await staff.PostAsJsonAsync(U("/api/loans/Reader"), new ReaderPanelRequest("QH"), Json));
        Assert.Equal((true, false), (panel.HasOverdue, panel.CanBorrow));
        Assert.Contains("quá hạn", (await Error(await Checkout(staff, "QH", place.Id, "QH02"))).Detail, StringComparison.Ordinal);

        var overdue = await Read<CrudPage<LoanDto>>(await staff.PostAsJsonAsync(U("/api/loans/Search"), new LoanSearch { State = "overdue", CardNo = "qh" }, Json));
        Assert.Equal("QH01", Assert.Single(overdue.Items).Barcode);

        var returned = await Read<ReturnResult>(await staff.PostAsJsonAsync(U("/api/loans/Return"), new ReturnRequest("QH01"), Json));
        Assert.Equal(6, returned.OverdueDays);
        Assert.Equal(1, (await Read<CheckoutResult>(await Checkout(staff, "QH", place.Id, "QH02"))).Succeeded);
    }

    [Fact]
    public async Task Most_specific_policy_sets_loan_days_limit_and_renewals()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "Q3");
        await Read<LoanPolicyDto>(await staff.PostAsJsonAsync(U("/api/loan-policies/Add"), new LoanPolicyRequest(null, null, 10, null, null, 7), Json));
        await Read<LoanPolicyDto>(await staff.PostAsJsonAsync(U("/api/loan-policies/Add"), new LoanPolicyRequest(5, place.Id, 3, 1, 1, 5), Json));
        var dupe = await staff.PostAsJsonAsync(U("/api/loan-policies/Add"), new LoanPolicyRequest(5, place.Id, 7), Json);
        Assert.Equal((409, "POLICY_EXISTS"), ((await Error(dupe)).Status, (await Error(dupe)).Code));

        var mfn = Interlocked.Increment(ref _nextMfn);
        await Publish(Reader(tenantId, "GV1", typeId: 5), Reader(tenantId, "GV2", typeId: 6), Item(tenantId, "CS01", mfn), Item(tenantId, "CS02", mfn), Item(tenantId, "CS03", mfn));

        var first = await Read<CheckoutResult>(await Checkout(staff, "GV1", place.Id, "CS01", "CS02"));
        Assert.Equal(1, first.Succeeded);
        Assert.Contains("mượn đủ 1", first.Lines[1].Message, StringComparison.Ordinal);
        var loan = first.Lines[0].Loan!;
        Assert.Equal(3, (loan.DueAt - loan.LoanedAt).TotalDays);
        Assert.Equal(10, (await Read<CheckoutResult>(await Checkout(staff, "GV2", place.Id, "CS03"))).Lines[0].Loan is { } other
            ? (other.DueAt - other.LoanedAt).TotalDays : 0);

        Assert.Equal("REASON_REQUIRED", (await Error(await staff.PostAsJsonAsync(U("/api/loans/Renew"), new RenewRequest(loan.PublicId, " "), Json))).Code);
        var renewed = await Read<LoanDto>(await staff.PostAsJsonAsync(U("/api/loans/Renew"), new RenewRequest(loan.PublicId, "Ôn thi"), Json));
        Assert.Equal((1, 5.0), (renewed.RenewCount, (renewed.DueAt - loan.DueAt).TotalDays));
        Assert.Equal("RENEW_LIMIT", (await Error(await staff.PostAsJsonAsync(U("/api/loans/Renew"), new RenewRequest(loan.PublicId, "Lần 2"), Json))).Code);

        var noted = await Read<LoanDto>(await staff.PostAsJsonAsync(U("/api/loans/Note"), new LoanNoteRequest(loan.PublicId, "Sách rách bìa", "Kiểm tra khi trả"), Json));
        Assert.Equal("Sách rách bìa", noted.Note);
    }

    [Fact]
    public async Task Missing_replicas_are_fetched_from_source_services_and_desk_store_rules_apply()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var desk = await AddPlace(staff, "Q4", 7);
        var mfn = Interlocked.Increment(ref _nextMfn);

        // Có ở patron/holdings/catalog nhưng circulation chưa nhận event (service mới triển khai).
        _factory.Sources.Readers[(tenantId, "CU-01")] = Reader(tenantId, "CU-01");
        _factory.Sources.Items[(tenantId, "CU0001")] = Item(tenantId, "CU0001", mfn, storeId: 7);
        _factory.Sources.Items[(tenantId, "CU0002")] = Item(tenantId, "CU0002", mfn, storeId: 8);
        _factory.Sources.Bibs[(tenantId, mfn)] = Bib(tenantId, mfn, "Tắt đèn");

        var result = await Read<CheckoutResult>(await Checkout(staff, "cu-01", desk.Id, "cu0001", "CU0002"));
        Assert.Equal(1, result.Succeeded);
        Assert.Equal("Tắt đèn", result.Lines[0].Loan!.Title);
        Assert.Contains("không mượn tại Quầy Q4", result.Lines[1].Message, StringComparison.Ordinal);

        // Đơn vị khác không thấy lượt mượn; nhân viên chỉ có quyền lịch sử thì không mượn được.
        var other = Staff(NewTenant());
        Assert.Equal(0, (await Read<CrudPage<LoanDto>>(await other.PostAsJsonAsync(U("/api/loans/Search"), new LoanSearch(), Json))).TotalCount);
        var historian = Staff(tenantId, "LOAN_HISTORY:view");
        Assert.Equal(1, (await Read<CrudPage<LoanDto>>(await historian.PostAsJsonAsync(U("/api/loans/Search"), new LoanSearch { Keyword = "tắt" }, Json))).TotalCount);
        Assert.Equal(HttpStatusCode.Forbidden, (await Checkout(historian, "CU-01", desk.Id, "CU0002")).StatusCode);

        var unlicensed = Staff(NewTenant(licensed: false));
        Assert.Equal(HttpStatusCode.Forbidden, (await unlicensed.PostAsJsonAsync(U("/api/loans/Search"), new LoanSearch(), Json)).StatusCode);
    }
}
