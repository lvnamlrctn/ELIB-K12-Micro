using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Catalog;
using Elib.Serials.Application;
using Elib.Serials.Domain;
using Elib.Serials.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Serials.Tests.Api;

public sealed class BindingAndReportTests(SerialsApiFactory factory) : SerialsTestBase(factory)
{
    /// <summary>Đăng ký có biểu ghi, sinh <paramref name="issues"/> kỳ, nhận <paramref name="received"/> kỳ đầu.</summary>
    private async Task<(SubscriptionDto Sub, IReadOnlyList<IssueDto> Issues)> Received(HttpClient staff, long tenantId, int issues = 4, int received = 3)
    {
        var bib = await PublishBib(tenantId, "Tạp chí Văn nghệ");
        var (sub, _, _) = await MonthlySubscription(staff, bib.BibPublicId);
        var predicted = (await Predict(staff, sub.PublicId, issues)).Issues;
        for (var i = 0; i < received; i++)
            await Read<IssueDto>(await Post(staff, "/api/issues/Receive", new IssueReceiveRequest(predicted[i].PublicId, new DateOnly(2026, 1 + i, 20))));
        return (sub, predicted);
    }

    [Fact]
    public async Task Binding_registers_a_real_item_at_holdings_once_and_each_issue_belongs_to_one_binding()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var (sub, issues) = await Received(staff, tenantId);

        var request = new BindingSaveRequest(sub.PublicId, [issues[0].PublicId, issues[1].PublicId], AccessionNo: "TC0001", VolumeTitle: "Tập 1/2026", StoreId: 1, StoreName: "Kho chính");
        var created = await Read<BindingDto>(await Post(staff, "/api/bindings/Add", request));
        Assert.Equal(("TC0001", 2, true), (created.AccessionNo, created.IssueCount, created.ItemPublicId is not null));
        Assert.Equal(["Số 1/2026", "Số 2/2026"], created.Issues.Select(i => i.Label));
        Assert.Equal(1, Factory.Holdings.Count(tenantId));

        // Kỳ đã vào tập: không đóng vào tập khác, không xoá, không ghép số.
        Assert.Equal("ISSUE_ALREADY_BOUND", await ErrorCode(await Post(staff, "/api/bindings/Add", request with { AccessionNo = "TC0002", IssueIds = [issues[1].PublicId, issues[2].PublicId] })));
        Assert.Equal("ISSUE_BOUND", await ErrorCode(await staff.DeleteAsync(U($"/api/issues/Delete/{issues[0].PublicId}"))));
        Assert.Equal("ISSUE_BOUND", await ErrorCode(await Post(staff, "/api/issues/Merge", new IssueMergeRequest(sub.PublicId, [issues[0].PublicId, issues[2].PublicId]))));
        Assert.Equal(1, Factory.Holdings.Count(tenantId));

        // Số ĐKCB trùng tập đã có; kỳ chưa nhận không đóng được; không chọn kỳ nào lỗi.
        Assert.Equal("ACCESSION_EXISTS", await ErrorCode(await Post(staff, "/api/bindings/Add", request with { IssueIds = [issues[2].PublicId] })));
        Assert.Equal("ISSUE_NOT_RECEIVED", await ErrorCode(await Post(staff, "/api/bindings/Add", request with { AccessionNo = "TC0003", IssueIds = [issues[3].PublicId] })));
        Assert.Equal("BINDING_ISSUES_REQUIRED", await ErrorCode(await Post(staff, "/api/bindings/Add", request with { AccessionNo = "TC0004", IssueIds = [] })));

        var found = await Read<CrudPage<BindingDto>>(await Post(staff, "/api/bindings/Search", new BindingSearch { AccessionNo = "TC00" }));
        Assert.Equal(created.PublicId, Assert.Single(found.Items).PublicId);

        // Sửa: đổi nhan đề tập và danh sách kỳ; số ĐKCB đã gắn bản sách nên khoá.
        var updated = await Read<BindingDto>(await staff.PutAsJsonAsync(U($"/api/bindings/Update/{created.PublicId}"),
            request with { VolumeTitle = "Tập 1 (sửa)", IssueIds = [issues[0].PublicId, issues[2].PublicId] }, Json));
        Assert.Equal("Tập 1 (sửa)", updated.VolumeTitle);
        Assert.Equal(["Số 1/2026", "Số 3/2026"], updated.Issues.Select(i => i.Label));
        Assert.Equal("BINDING_ACCESSION_LOCKED", await ErrorCode(await staff.PutAsJsonAsync(U($"/api/bindings/Update/{created.PublicId}"),
            request with { AccessionNo = "KHAC" }, Json)));
        var boundOnly = await Read<CrudPage<IssueDto>>(await Post(staff, "/api/issues/Search", new IssueSearch { SubscriptionPublicId = sub.PublicId, Bound = true }));
        Assert.Equal(2, boundOnly.TotalCount);
    }

    [Fact]
    public async Task Binding_needs_a_bib_and_holdings_and_a_holdings_failure_saves_nothing()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var (sub, issues) = await Received(staff, tenantId);

        var noBib = await AddSubscription(staff, new SubscriptionRequest("Báo không biểu ghi", FrequencyId: sub.FrequencyId, PatternId: sub.PatternId, FirstIssueDate: new DateOnly(2026, 1, 1)));
        var loose = (await Predict(staff, noBib.PublicId, 1)).Issues[0];
        await Post(staff, "/api/issues/Receive", new IssueReceiveRequest(loose.PublicId));
        Assert.Equal("SERIAL_BIB_REQUIRED", await ErrorCode(await Post(staff, "/api/bindings/Add", new BindingSaveRequest(noBib.PublicId, [loose.PublicId], AccessionNo: "KB1", VolumeTitle: "Tập"))));

        Factory.Holdings.Down = true;
        try
        {
            var down = await Post(staff, "/api/bindings/Add", new BindingSaveRequest(sub.PublicId, [issues[0].PublicId], AccessionNo: "DOWN1", VolumeTitle: "Tập"));
            Assert.Equal((HttpStatusCode.ServiceUnavailable, "HOLDINGS_UNAVAILABLE"), (down.StatusCode, await ErrorCode(down)));
        }
        finally
        {
            Factory.Holdings.Down = false;
        }
        Assert.Equal(0, (await Read<CrudPage<BindingDto>>(await Post(staff, "/api/bindings/Search", new BindingSearch()))).TotalCount);
        // Số ĐKCB đã có tập khác: serials chặn trước khi gọi holdings.
        await Read<BindingDto>(await Post(staff, "/api/bindings/Add", new BindingSaveRequest(sub.PublicId, [issues[0].PublicId], AccessionNo: "DUP1", VolumeTitle: "Tập A")));
        var otherSub = await Received(staff, tenantId, 2, 1);
        Assert.Equal("ACCESSION_EXISTS", await ErrorCode(await Post(staff, "/api/bindings/Add",
            new BindingSaveRequest(otherSub.Sub.PublicId, [otherSub.Issues[0].PublicId], AccessionNo: "DUP1", VolumeTitle: "Tập B"))));
    }

    [Fact]
    public async Task When_serials_cannot_save_after_holdings_created_the_item_the_item_is_removed_again()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var (sub, issues) = await Received(staff, tenantId);

        // Ngay sau khi holdings tạo ĐKCB, một tập khác chiếm mất số ĐKCB → lưu serials đụng chỉ mục duy nhất.
        Factory.Holdings.AfterRegister = (tenant, registered) =>
        {
            using var scope = Factory.Services.CreateScope();
            using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenant);
            var db = scope.ServiceProvider.GetRequiredService<SerialsDbContext>();
            db.Set<Binding>().Add(Binding.Create(Guid.CreateVersion7(), new BindingData("RACE1", "Tập chen ngang", sub.Id), new DateOnly(2026, 1, 1)));
            db.SaveChanges();
        };
        try
        {
            var response = await Post(staff, "/api/bindings/Add", new BindingSaveRequest(sub.PublicId, [issues[0].PublicId], AccessionNo: "RACE1", VolumeTitle: "Tập"));
            Assert.False(response.IsSuccessStatusCode);
        }
        finally
        {
            Factory.Holdings.AfterRegister = null;
        }
        Assert.Equal(0, Factory.Holdings.Count(tenantId));
        Assert.True(Factory.Holdings.RemoveCalls >= 1);
    }

    [Fact]
    public async Task Deleting_a_binding_removes_the_item_first_and_a_blocked_removal_keeps_the_binding()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var (sub, issues) = await Received(staff, tenantId);
        var created = await Read<BindingDto>(await Post(staff, "/api/bindings/Add", new BindingSaveRequest(sub.PublicId, [issues[0].PublicId], AccessionNo: "DEL1", VolumeTitle: "Tập")));

        Factory.Holdings.BlockRemove = true;
        try
        {
            Assert.Equal("ITEM_ON_LOAN", await ErrorCode(await staff.DeleteAsync(U($"/api/bindings/Delete/{created.PublicId}"))));
        }
        finally
        {
            Factory.Holdings.BlockRemove = false;
        }
        Assert.Equal(created.PublicId, (await Read<BindingDto>(await staff.GetAsync(U($"/api/bindings/GetById/{created.PublicId}")))).PublicId);

        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/bindings/Delete/{created.PublicId}"))).StatusCode);
        Assert.False(Factory.Holdings.Has(tenantId, created.PublicId));
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync(U($"/api/bindings/GetById/{created.PublicId}"))).StatusCode);
        // Kỳ được giải phóng: xoá được, và đóng lại cùng số ĐKCB được.
        await Read<BindingDto>(await Post(staff, "/api/bindings/Add", new BindingSaveRequest(sub.PublicId, [issues[0].PublicId], AccessionNo: "DEL1", VolumeTitle: "Tập mới")));
    }

    [Fact]
    public async Task Reports_summarise_received_issues_list_the_details_and_missing_claims_and_export_excel()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var (sub, issues) = await Received(staff, tenantId, issues: 5, received: 3);
        await Post(staff, "/api/issues/Receive", new IssueReceiveRequest(issues[1].PublicId, new DateOnly(2026, 2, 28), 2)); // đã nhận, nhận lại với 2 bản
        await Post(staff, "/api/issues/Claim", new IssueClaimRequest(issues[3].PublicId));

        var summary = await Read<CrudPage<ReceivedSummaryRow>>(await Post(staff, "/api/reports/ReceivedSummary", new SerialReportRequest(sub.PublicId)));
        var row = Assert.Single(summary.Items);
        Assert.Equal((3, 4, new DateOnly(2026, 3, 20)), (row.IssueCount, row.Quantity, row.LastReceivedDate!.Value));
        var feb = await Read<CrudPage<ReceivedSummaryRow>>(await Post(staff, "/api/reports/ReceivedSummary",
            new SerialReportRequest(Keyword: "thử", From: new DateOnly(2026, 2, 1), To: new DateOnly(2026, 2, 28))));
        Assert.Equal((1, 2), (Assert.Single(feb.Items).IssueCount, feb.Items[0].Quantity));

        var detail = await Read<CrudPage<ReceivedDetailRow>>(await Post(staff, "/api/reports/ReceivedDetail", new SerialReportRequest(sub.PublicId)));
        Assert.Equal(3, detail.TotalCount);
        Assert.Equal(["Số 1/2026", "Số 2/2026", "Số 3/2026"], detail.Items.Select(d => d.Label));
        Assert.True(detail.Items[0].Late); // dự kiến 15/01, nhận 20/01

        var missing = await Read<CrudPage<MissingClaimRow>>(await Post(staff, "/api/reports/MissingClaim", new SerialReportRequest(sub.PublicId)));
        Assert.Contains(missing.Items, m => m.Label == "Số 4/2026" && m.Status == IssueStatus.Claimed && m.ClaimCount == 1);
        Assert.Contains(missing.Items, m => m.Label == "Số 5/2026");
        Assert.DoesNotContain(missing.Items, m => m.Label == "Số 1/2026");

        foreach (var export in new[] { "ReceivedSummaryExport", "ReceivedDetailExport", "MissingClaimExport" })
        {
            var response = await Post(staff, "/api/reports/" + export, new SerialReportRequest(sub.PublicId));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(CrudExcel.ContentType, response.Content.Headers.ContentType?.MediaType);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.True(bytes.Length > 1000);
            Assert.Equal((byte)'P', bytes[0]); // xlsx = zip (PK)
        }

        var dash = await Read<SerialsDashboard>(await staff.GetAsync(U("/api/dashboard")));
        Assert.Equal((1, 5, 3, 1), (dash.Subscriptions, dash.Issues, dash.Received, dash.Claimed));
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(Staff(tenantId, "SUBSCRIPTIONS:view"), "/api/reports/MissingClaim", new SerialReportRequest())).StatusCode);
    }

    [Fact]
    public async Task Suppliers_are_optional_and_bib_events_keep_the_snapshot_current()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        Assert.Empty(await Read<IReadOnlyList<SupplierOption>>(await staff.GetAsync(U("/api/suppliers"))));
        Factory.Suppliers.ByTenant[tenantId] = [new SupplierOption(Guid.NewGuid(), "Bưu điện")];
        Assert.Equal("Bưu điện", Assert.Single(await Read<IReadOnlyList<SupplierOption>>(await staff.GetAsync(U("/api/suppliers")))).Name);

        var bib = await PublishBib(tenantId, "Báo Hà Nội mới");
        var renamed = bib with { Title = "Báo Hà Nội Mới (đổi tên)", Version = 5, EventId = Guid.CreateVersion7() };
        await Factory.PublishAsync(renamed);
        await Factory.WaitConsumedAsync(renamed);
        var older = bib with { Title = "Bản cũ", Version = 2, EventId = Guid.CreateVersion7() };
        await Factory.PublishAsync(older);
        await Factory.WaitConsumedAsync(older);
        var options = await Read<IReadOnlyList<BibOption>>(await Post(staff, "/api/bibs/Lookup", new CrudSearch { Keyword = "ha noi moi" }));
        Assert.Equal("Báo Hà Nội Mới (đổi tên)", Assert.Single(options).Title);

        // Tra theo MFN chưa có bản sao → hỏi catalog.
        var remote = new BibChanged { TenantId = tenantId, BibPublicId = Guid.CreateVersion7(), Mfn = 880_001, Title = "Tạp chí Xưa & Nay", Status = 2 };
        Factory.CatalogBibs.Bibs[(tenantId, remote.Mfn)] = remote;
        Assert.Equal("Tạp chí Xưa & Nay", Assert.Single(await Read<IReadOnlyList<BibOption>>(await Post(staff, "/api/bibs/Lookup", new CrudSearch { Keyword = "880001" }))).Title);
    }
}
