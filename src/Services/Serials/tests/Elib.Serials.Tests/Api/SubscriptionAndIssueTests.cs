using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.Serials.Application;
using Elib.Serials.Domain;

namespace Elib.Serials.Tests.Api;

public sealed class SubscriptionAndIssueTests(SerialsApiFactory factory) : SerialsTestBase(factory)
{
    [Fact]
    public async Task Subscription_can_be_declared_by_hand_but_approval_needs_a_bib_and_locks_edit_and_delete()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var (sub, _, _) = await MonthlySubscription(staff);
        Assert.Equal(("Tạp chí thử", "1234-5678", null, false), (sub.Title, sub.Issn, sub.BibMfn, sub.Approved));
        Assert.Equal(("Tháng", 0), (sub.FrequencyName?.Split(' ')[0], sub.IssueCount));

        Assert.Equal("SERIAL_BIB_REQUIRED", await ErrorCode(await staff.PostAsync(U($"/api/subscriptions/Approve/{sub.PublicId}"), null)));

        var bib = await PublishBib(tenantId, "Tạp chí Cộng sản");
        var linked = await Read<SubscriptionDto>(await staff.PostAsync(U($"/api/subscriptions/LinkBib/{sub.PublicId}/{bib.BibPublicId}"), null));
        Assert.Equal((bib.Mfn, bib.BibPublicId), (linked.BibMfn, linked.BibPublicId));
        var approved = await Read<SubscriptionDto>(await staff.PostAsync(U($"/api/subscriptions/Approve/{sub.PublicId}"), null));
        Assert.True(approved.Approved);
        Assert.NotNull(approved.ApprovedAt);

        var update = new SubscriptionRequest("Đổi tên", BibPublicId: bib.BibPublicId);
        Assert.Equal("SUBSCRIPTION_APPROVED", await ErrorCode(await staff.PutAsJsonAsync(U($"/api/subscriptions/Update/{sub.PublicId}"), update, Json)));
        Assert.Equal("SUBSCRIPTION_APPROVED", await ErrorCode(await staff.DeleteAsync(U($"/api/subscriptions/Delete/{sub.PublicId}"))));
        Assert.Equal("SUBSCRIPTION_APPROVED", await ErrorCode(await staff.PostAsync(U($"/api/subscriptions/Approve/{sub.PublicId}"), null)));
    }

    [Fact]
    public async Task Subscription_with_a_bib_takes_its_title_and_search_returns_joined_names_and_counts()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Báo Thanh Niên");
        var frequency = await AddFrequency(staff, "Hằng ngày " + tenantId, FrequencyUnit.Day);
        var pattern = await AddPattern(staff, "Số " + tenantId, "Số {X}", maxX: null);
        var sub = await AddSubscription(staff, new SubscriptionRequest(BibPublicId: bib.BibPublicId, FrequencyId: frequency.Id, PatternId: pattern.Id,
            FirstIssueDate: new DateOnly(2026, 3, 1), StoreId: 3, StoreName: "Kho báo", SupplierName: "Bưu điện"));
        Assert.Equal(("Báo Thanh Niên", bib.Mfn, "Kho báo"), (sub.Title, sub.BibMfn, sub.StoreName));
        Assert.Equal("TITLE_INVALID", await ErrorCode(await Post(staff, "/api/subscriptions/Add", new SubscriptionRequest())));
        Assert.Equal("FREQUENCY_NOT_FOUND", await ErrorCode(await Post(staff, "/api/subscriptions/Add", new SubscriptionRequest("X", FrequencyId: 99999))));

        await Predict(staff, sub.PublicId, 3);
        var page = await Read<CrudPage<SubscriptionDto>>(await Post(staff, "/api/subscriptions/Search", new SubscriptionSearch { Keyword = "thanh niên" }));
        var row = Assert.Single(page.Items);
        Assert.Equal((frequency.Name, pattern.Name, 3, 0), (row.FrequencyName, row.PatternName, row.IssueCount, row.ReceivedCount));
        Assert.Empty((await Read<CrudPage<SubscriptionDto>>(await Post(staff, "/api/subscriptions/Search", new SubscriptionSearch { Approved = true }))).Items);
    }

    [Fact]
    public async Task Predict_generates_numbered_issues_with_planned_dates_and_continues_from_the_last_one()
    {
        var staff = Staff(NewTenant());
        var (sub, _, _) = await MonthlySubscription(staff, startX: 11);

        var first = await Predict(staff, sub.PublicId, 4);
        Assert.Equal((4, 0), (first.Generated, first.Skipped));
        Assert.Equal(["Số 11/2026", "Số 12/2026", "Số 1/2027", "Số 2/2027"], first.Issues.Select(i => i.Label));
        // Kỳ đầu đúng ngày phát hành kỳ đầu (15/01/2026), các kỳ sau cách một tháng tính từ mốc.
        Assert.Equal([new DateOnly(2026, 1, 15), new(2026, 2, 15), new(2026, 3, 15), new(2026, 4, 15)], first.Issues.Select(i => i.PlannedDate!.Value));
        Assert.All(first.Issues, i => Assert.Equal(IssueStatus.Planned, i.Status));

        var second = await Predict(staff, sub.PublicId, 2);
        Assert.Equal(["Số 3/2027", "Số 4/2027"], second.Issues.Select(i => i.Label));
        Assert.Equal(new DateOnly(2026, 5, 15), second.Issues[0].PlannedDate);

        var current = await Read<SubscriptionDto>(await staff.GetAsync(U($"/api/subscriptions/GetById/{sub.PublicId}")));
        Assert.Equal((4, 2027), (current.LastX, current.LastY));
        Assert.Equal(6, current.IssueCount);
    }

    [Fact]
    public async Task Predict_skips_numbers_that_already_have_an_issue_and_clamps_the_count_and_needs_pattern_and_frequency()
    {
        var staff = Staff(NewTenant());
        var (sub, _, _) = await MonthlySubscription(staff);
        // Nhập tay số 2/2026 trước khi sinh.
        await Read<IssueDto>(await Post(staff, "/api/issues/Save", new IssueSaveRequest(sub.PublicId, X: 2, Y: 2026, PlannedDate: new DateOnly(2026, 2, 20))));

        var result = await Predict(staff, sub.PublicId, 3);
        Assert.Equal((3, 1), (result.Generated, result.Skipped));
        Assert.Equal(["Số 1/2026", "Số 3/2026", "Số 4/2026"], result.Issues.Select(i => i.Label).Order());

        var big = await Predict(staff, sub.PublicId, 100_000);
        Assert.Equal(IssueService.MaxPredict, big.Generated);

        var bare = await AddSubscription(staff, new SubscriptionRequest("Không mẫu"));
        Assert.Equal("SERIAL_PATTERN_REQUIRED", await ErrorCode(await Post(staff, "/api/issues/Predict", new IssuePredictRequest(bare.PublicId, 3))));
    }

    [Fact]
    public async Task Manual_save_with_a_planned_number_updates_that_issue_instead_of_duplicating_and_receiving_sets_the_date()
    {
        var staff = Staff(NewTenant());
        var (sub, _, _) = await MonthlySubscription(staff);
        var predicted = await Predict(staff, sub.PublicId, 3);
        var planned = predicted.Issues[1]; // Số 2/2026

        var saved = await Read<IssueDto>(await Post(staff, "/api/issues/Save",
            new IssueSaveRequest(sub.PublicId, X: 2, Y: 2026, Status: IssueStatus.Received, Quantity: 3, Note: "Có phụ trương")));
        Assert.Equal(planned.PublicId, saved.PublicId);
        Assert.Equal((IssueStatus.Received, 3, "Có phụ trương"), (saved.Status, saved.Quantity, saved.Note));
        Assert.NotNull(saved.ReceivedDate);
        Assert.Equal(planned.PlannedDate, saved.PlannedDate);

        var all = await Read<CrudPage<IssueDto>>(await Post(staff, "/api/issues/Search", new IssueSearch { SubscriptionPublicId = sub.PublicId }));
        Assert.Equal(3, all.TotalCount);

        // Số đặc biệt thêm mới có nhãn tự nhập; không nhãn và không số thì lỗi.
        var special = await Read<IssueDto>(await Post(staff, "/api/issues/Save", new IssueSaveRequest(sub.PublicId, Label: "Số Tết", IsSpecial: true)));
        Assert.True(special.IsSpecial);
        Assert.Equal("LABEL_INVALID", await ErrorCode(await Post(staff, "/api/issues/Save", new IssueSaveRequest(sub.PublicId))));

        var received = await Read<IssueDto>(await Post(staff, "/api/issues/Receive", new IssueReceiveRequest(predicted.Issues[0].PublicId, new DateOnly(2026, 1, 20), 2)));
        Assert.Equal((IssueStatus.Received, new DateOnly(2026, 1, 20), 2), (received.Status, received.ReceivedDate!.Value, received.Quantity));
    }

    [Fact]
    public async Task Claim_is_blocked_for_received_issues_and_respects_the_grace_days_and_stats_count_missing_and_late()
    {
        var staff = Staff(NewTenant());
        var frequency = await AddFrequency(staff, "T " + Guid.NewGuid().ToString("N")[..5], FrequencyUnit.Month);
        var pattern = await AddPattern(staff, "M " + Guid.NewGuid().ToString("N")[..5], "Số {X}", maxX: null);
        var sub = await AddSubscription(staff, new SubscriptionRequest("Báo trễ", FrequencyId: frequency.Id, PatternId: pattern.Id, FirstIssueDate: new DateOnly(2020, 1, 1), GraceDays: 5));
        var issues = (await Predict(staff, sub.PublicId, 3)).Issues; // 2020 — đã quá hạn từ lâu

        var claimed = await Read<IssueDto>(await Post(staff, "/api/issues/Claim", new IssueClaimRequest(issues[0].PublicId)));
        Assert.Equal((IssueStatus.Claimed, 1), (claimed.Status, claimed.ClaimCount));
        Assert.Equal(2, (await Read<IssueDto>(await Post(staff, "/api/issues/Claim", new IssueClaimRequest(issues[0].PublicId)))).ClaimCount);

        await Post(staff, "/api/issues/Receive", new IssueReceiveRequest(issues[1].PublicId, new DateOnly(2020, 2, 10)));
        Assert.Equal("ISSUE_RECEIVED", await ErrorCode(await Post(staff, "/api/issues/Claim", new IssueClaimRequest(issues[1].PublicId))));

        var stats = await Read<IssueStatistics>(await staff.GetAsync(U($"/api/issues/Statistics/{sub.PublicId}")));
        // 3 kỳ: 1 khiếu nại, 1 đã nhận (nhận 10/02 sau dự kiến 01/02 → trễ), 1 dự kiến quá hạn (thiếu).
        Assert.Equal((3, 1, 1, 1, 1, (DateOnly?)new DateOnly(2020, 2, 10)), (stats.Total, stats.Received, stats.Claimed, stats.Missing, stats.Late, stats.LastReceivedDate));

        // Chưa quá hạn chờ: kỳ dự kiến ngày mai với 5 ngày chờ → chưa khiếu nại được.
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7).AddDays(1));
        var future = await Read<IssueDto>(await Post(staff, "/api/issues/Save", new IssueSaveRequest(sub.PublicId, Label: "Sắp ra", IsSpecial: true, PlannedDate: tomorrow)));
        Assert.Equal("CLAIM_TOO_EARLY", await ErrorCode(await Post(staff, "/api/issues/Claim", new IssueClaimRequest(future.PublicId))));
    }

    [Fact]
    public async Task Merge_combines_issues_into_one_received_issue_in_a_single_step_and_a_bad_selection_changes_nothing()
    {
        var staff = Staff(NewTenant());
        var (sub, _, _) = await MonthlySubscription(staff);
        var (other, _, _) = await MonthlySubscription(staff);
        var issues = (await Predict(staff, sub.PublicId, 4)).Issues;
        var foreign = (await Predict(staff, other.PublicId, 1)).Issues[0];

        // Chọn lẫn kỳ của đăng ký khác → lỗi, không kỳ nào bị xoá.
        var bad = await Post(staff, "/api/issues/Merge", new IssueMergeRequest(sub.PublicId, [issues[0].PublicId, foreign.PublicId]));
        Assert.Equal("ISSUE_SUBSCRIPTION_MISMATCH", await ErrorCode(bad));
        Assert.Equal("MERGE_MIN_TWO", await ErrorCode(await Post(staff, "/api/issues/Merge", new IssueMergeRequest(sub.PublicId, [issues[0].PublicId]))));
        Assert.Equal(4, (await Read<CrudPage<IssueDto>>(await Post(staff, "/api/issues/Search", new IssueSearch { SubscriptionPublicId = sub.PublicId }))).TotalCount);

        var merged = await Read<IssueDto>(await Post(staff, "/api/issues/Merge", new IssueMergeRequest(sub.PublicId, [issues[2].PublicId, issues[1].PublicId])));
        Assert.Equal(("Số 2+3/2026", true, IssueStatus.Received, 2), (merged.Label, merged.IsMerged, merged.Status, merged.Quantity));
        Assert.Equal(issues[1].PlannedDate, merged.PlannedDate);
        var left = await Read<CrudPage<IssueDto>>(await Post(staff, "/api/issues/Search", new IssueSearch { SubscriptionPublicId = sub.PublicId }));
        Assert.Equal(3, left.TotalCount);
        Assert.DoesNotContain(left.Items, i => i.PublicId == issues[1].PublicId || i.PublicId == issues[2].PublicId);

        // Sinh tiếp không đụng chỗ kỳ ghép (kỳ ghép không tính vào chỉ mục trùng số).
        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/issues/Delete/{issues[3].PublicId}"))).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_subscription_removes_its_issues_and_unapproved_edit_works()
    {
        var staff = Staff(NewTenant());
        var (sub, _, _) = await MonthlySubscription(staff);
        await Predict(staff, sub.PublicId, 2);
        var edited = await Read<SubscriptionDto>(await staff.PutAsJsonAsync(U($"/api/subscriptions/Update/{sub.PublicId}"),
            new SubscriptionRequest("Tên mới", GraceDays: 7, FrequencyId: sub.FrequencyId, PatternId: sub.PatternId), Json));
        Assert.Equal(("Tên mới", 7), (edited.Title, edited.GraceDays));
        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/subscriptions/Delete/{sub.PublicId}"))).StatusCode);
        Assert.Equal(0, (await Read<CrudPage<IssueDto>>(await Post(staff, "/api/issues/Search", new IssueSearch { SubscriptionPublicId = sub.PublicId }))).TotalCount);
    }
}
