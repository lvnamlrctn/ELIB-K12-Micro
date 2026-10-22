using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elib.BuildingBlocks.Crud;
using Elib.Circulation.Application;
using Elib.Circulation.Domain;
using Elib.Contracts.Events.Payment;
using Elib.Contracts.Events.Platform;

namespace Elib.Circulation.Tests.Api;

/// <summary>Kiểm soát thao tác phiếu phạt: lý do bắt buộc, trước/sau trong nhật ký, sổ thanh toán, thẩm quyền phê duyệt.</summary>
public sealed class FineControlsApiTests(CirculationApiFactory factory) : CirculationTestBase(factory)
{
    private static readonly string[] CashierGrants = ["FINES:view", "FINES:add", "FINES:edit", "FINES:delete"];

    private async Task<(long TenantId, HttpClient Admin, HttpClient Cashier, FineTicketDto Ticket)> Setup(string card, decimal amount = 100_000)
    {
        var tenantId = NewTenant();
        var admin = Staff(tenantId);
        var cashier = Staff(tenantId, CashierGrants);
        await Publish(Reader(tenantId, card));
        var ticket = await Read<FineTicketDto>(await admin.PostAsJsonAsync(U("/api/fine-tickets/Add"), new FineTicketCreateRequest(card, amount, "Phạt thủ công"), Json));
        return (tenantId, admin, cashier, ticket);
    }

    private static SaveFineTicketRequest Save(FineTicketDto t, decimal? discount = null, decimal? paid = null, int? status = null, string? reason = null) =>
        new(status ?? t.Status, discount ?? t.Discount, paid ?? t.Paid, t.Note, Reason: reason);

    private static Task<HttpResponseMessage> Put(HttpClient c, FineTicketDto t, SaveFineTicketRequest r) => c.PutAsJsonAsync(U($"/api/fine-tickets/Save/{t.PublicId}"), r, Json);

    [Fact]
    public async Task Changing_the_discount_needs_a_reason_and_the_audit_entry_holds_before_and_after()
    {
        var (tenantId, admin, _, ticket) = await Setup("KS-01");

        Assert.Equal("REASON_REQUIRED", (await Error(await Put(admin, ticket, Save(ticket, discount: 10_000)))).Code);
        var saved = await Read<FineSaveDetail>(await Put(admin, ticket, Save(ticket, discount: 10_000, reason: "Hoàn cảnh khó khăn")));
        Assert.Equal((90_000m, 10_000m), (saved.Ticket.Remaining, saved.Ticket.Discount));

        var entry = Factory.PublishedOf<AuditRecorded>().Last(a => a.TenantId == tenantId && a.EntityType == "FineTicket" && a.Action == "UPDATE");
        using var json = JsonDocument.Parse(entry.ChangesJson!);
        Assert.Equal("Hoàn cảnh khó khăn", json.RootElement.GetProperty("_reason").GetString());
        var discount = json.RootElement.GetProperty("discount");
        Assert.Equal((0m, 10_000m), (discount[0].GetDecimal(), discount[1].GetDecimal()));
        Assert.Equal(100_000m, json.RootElement.GetProperty("remaining")[0].GetDecimal());
    }

    [Fact]
    public async Task Cash_is_kept_as_immutable_payments_never_reduced_directly_and_never_over_the_total()
    {
        var (_, admin, _, ticket) = await Setup("KS-02");

        var first = await Read<FineSaveDetail>(await Put(admin, ticket, Save(ticket, paid: 40_000)));
        var second = await Read<FineSaveDetail>(await Put(admin, first.Ticket, Save(first.Ticket, paid: 100_000)));
        Assert.Equal([40_000m, 60_000m], second.Payments!.Select(p => p.Amount).ToArray());
        Assert.Equal((100_000m, 0m), (second.Ticket.Paid, second.Ticket.Remaining));

        Assert.Equal("FINE_PAID_DECREASE", (await Error(await Put(admin, second.Ticket, Save(second.Ticket, paid: 10_000)))).Code);
        Assert.Equal("FINE_OVERPAID", (await Error(await Put(admin, second.Ticket, Save(second.Ticket, paid: 120_000)))).Code);

        // Điều chỉnh: khoản âm có lý do, số cũ giữ nguyên.
        Assert.Equal("REASON_REQUIRED", (await Error(await admin.PostAsJsonAsync(U($"/api/fine-tickets/Adjust/{ticket.PublicId}"), new AdjustFinePaymentRequest(-30_000, ""), Json))).Code);
        Assert.Equal("FINE_ADJUST_TOO_BIG", (await Error(await admin.PostAsJsonAsync(U($"/api/fine-tickets/Adjust/{ticket.PublicId}"), new AdjustFinePaymentRequest(-130_000, "Hoàn"), Json))).Code);
        var adjusted = await Read<FineSaveDetail>(await admin.PostAsJsonAsync(U($"/api/fine-tickets/Adjust/{ticket.PublicId}"),
            new AdjustFinePaymentRequest(-30_000, "Thu nhầm 30.000đ, hoàn lại bạn đọc"), Json));
        Assert.Equal((70_000m, 30_000m), (adjusted.Ticket.Paid, adjusted.Ticket.Remaining));
        Assert.Equal([40_000m, 60_000m, -30_000m], adjusted.Payments!.Select(p => p.Amount).ToArray());
        Assert.Equal("ADJUST", adjusted.Payments![2].Method);
    }

    [Fact]
    public async Task A_discount_over_the_threshold_by_a_cashier_is_held_for_approval_and_nothing_changes_until_it_is_approved()
    {
        var (tenantId, admin, cashier, ticket) = await Setup("KS-03");

        // 10% (≤ 20% và ≤ 100.000đ): áp dụng ngay.
        var small = await Put(cashier, ticket, Save(ticket, discount: 10_000, reason: "Làm tròn"));
        Assert.Equal(HttpStatusCode.OK, small.StatusCode);

        // 50%: vượt ngưỡng → 202, phiếu chưa đổi.
        var big = await Put(cashier, ticket, Save(ticket, discount: 50_000, reason: "Gia đình khó khăn"));
        Assert.Equal(HttpStatusCode.Accepted, big.StatusCode);
        var pending = await Read<PendingApproval>(big);
        Assert.Equal(FineApprovals.Discount, pending.Kind);
        Assert.Equal(10_000m, (await Read<FineTicketDetail>(await admin.GetAsync(U($"/api/fine-tickets/Detail/{ticket.PublicId}")))).Ticket.Discount);

        // Người đề nghị không duyệt được (không có FINES:approve), kể cả khi có quyền sửa.
        Assert.Equal("APPROVAL_FORBIDDEN", (await Error(await cashier.PostAsJsonAsync(U($"/api/approvals/{pending.RequestId}/Approve"), new ApprovalDecisionRequest("tự duyệt"), Json))).Code);

        var inbox = await Read<CrudPage<ChangeRequestDto>>(await admin.PostAsJsonAsync(U("/api/approvals/Search"), new ChangeRequestSearch { Scope = "inbox" }, Json));
        var request = Assert.Single(inbox.Items);
        Assert.Equal(("Gia đình khó khăn", true, ApprovalStatus.Pending), (request.Reason, request.CanDecide, request.Status));
        Assert.Equal(10_000m, request.Before!["discount"]!.GetValue<decimal>());
        Assert.Equal(50_000m, request.After!["discount"]!.GetValue<decimal>());
        // Người đề nghị không thấy trong hộp duyệt của mình (không có quyền), nhưng thấy ở "của tôi".
        Assert.Empty((await Read<CrudPage<ChangeRequestDto>>(await cashier.PostAsJsonAsync(U("/api/approvals/Search"), new ChangeRequestSearch { Scope = "inbox" }, Json))).Items);
        Assert.Single((await Read<CrudPage<ChangeRequestDto>>(await cashier.PostAsJsonAsync(U("/api/approvals/Search"), new ChangeRequestSearch { Scope = "mine" }, Json))).Items);

        var approved = await Read<ChangeRequestDto>(await admin.PostAsJsonAsync(U($"/api/approvals/{pending.RequestId}/Approve"), new ApprovalDecisionRequest("Đã xác minh"), Json));
        Assert.Equal(ApprovalStatus.Approved, approved.Status);
        var after = await Read<FineTicketDetail>(await admin.GetAsync(U($"/api/fine-tickets/Detail/{ticket.PublicId}")));
        Assert.Equal((50_000m, 50_000m), (after.Ticket.Discount, after.Ticket.Remaining));
        Assert.Empty(after.PendingRequests!);
        Assert.Contains(Factory.PublishedOf<AuditRecorded>(), a => a.TenantId == tenantId && a.Action == "APPROVE" && a.EntityType == "FineTicket");

        Assert.Equal("APPROVAL_NOT_PENDING", (await Error(await admin.PostAsJsonAsync(U($"/api/approvals/{pending.RequestId}/Approve"), new ApprovalDecisionRequest(), Json))).Code);
    }

    [Fact]
    public async Task Approving_fails_when_the_ticket_changed_since_the_request_and_rejecting_needs_a_note()
    {
        var (_, admin, cashier, ticket) = await Setup("KS-04");
        var first = await Read<PendingApproval>(await Put(cashier, ticket, Save(ticket, discount: 60_000, reason: "Miễn một phần")));

        // Phiếu đổi (thu thêm tiền) sau khi đề nghị → yêu cầu cũ không còn đúng.
        Assert.True((await Put(admin, ticket, Save(ticket, paid: 20_000))).IsSuccessStatusCode);
        Assert.Equal("APPROVAL_STALE", (await Error(await admin.PostAsJsonAsync(U($"/api/approvals/{first.RequestId}/Approve"), new ApprovalDecisionRequest(), Json))).Code);
        var failed = await Read<CrudPage<ChangeRequestDto>>(await admin.PostAsJsonAsync(U("/api/approvals/Search"), new ChangeRequestSearch { Scope = "inbox", Status = 5 }, Json));
        Assert.Equal(ApprovalStatus.Failed, Assert.Single(failed.Items).Status);

        var refreshed = await Read<FineTicketDetail>(await admin.GetAsync(U($"/api/fine-tickets/Detail/{ticket.PublicId}")));
        var second = await Read<PendingApproval>(await Put(cashier, refreshed.Ticket, Save(refreshed.Ticket, discount: 70_000, reason: "Xin miễn")));
        Assert.Equal("REASON_REQUIRED", (await Error(await admin.PostAsJsonAsync(U($"/api/approvals/{second.RequestId}/Reject"), new ApprovalDecisionRequest(" "), Json))).Code);
        var rejected = await Read<ChangeRequestDto>(await admin.PostAsJsonAsync(U($"/api/approvals/{second.RequestId}/Reject"), new ApprovalDecisionRequest("Không đủ căn cứ"), Json));
        Assert.Equal((ApprovalStatus.Rejected, "Không đủ căn cứ"), (rejected.Status, rejected.DecisionNote));
        Assert.Equal(0m, (await Read<FineTicketDetail>(await admin.GetAsync(U($"/api/fine-tickets/Detail/{ticket.PublicId}")))).Ticket.Discount);
    }

    [Fact]
    public async Task Reopening_a_finished_ticket_and_refunds_need_approval_and_the_rule_can_be_tuned_per_unit()
    {
        var (_, admin, cashier, ticket) = await Setup("KS-05");
        var paid = await Read<FineSaveDetail>(await Put(admin, ticket, Save(ticket, paid: 100_000, status: FineTicketStatus.Done)));

        // Mở lại phiếu đã hoàn thành → chờ duyệt (mặc định luôn cần duyệt).
        var reopen = await Put(cashier, paid.Ticket, Save(paid.Ticket, status: FineTicketStatus.Open, reason: "Nhập sai số tiền"));
        Assert.Equal(HttpStatusCode.Accepted, reopen.StatusCode);
        Assert.Equal(FineTicketStatus.Done, (await Read<FineTicketDetail>(await admin.GetAsync(U($"/api/fine-tickets/Detail/{ticket.PublicId}")))).Ticket.Status);

        // Hoàn tiền bởi người không có quyền duyệt → chờ duyệt; người duyệt hoàn thì áp dụng ngay.
        var refund = await cashier.PostAsJsonAsync(U($"/api/fine-tickets/Adjust/{ticket.PublicId}"), new AdjustFinePaymentRequest(-20_000, "Thu nhầm"), Json);
        Assert.Equal(HttpStatusCode.Accepted, refund.StatusCode);

        // Đơn vị tắt quy tắc mở lại → người thu ngân mở lại được ngay (chỉ cần lý do).
        var rules = await Read<List<ApprovalRuleDto>>(await admin.GetAsync(U("/api/approval-rules")));
        Assert.Contains(rules, r => r.Kind == FineApprovals.Reopen && r.Enabled && !r.IsCustom);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PutAsJsonAsync(U($"/api/approval-rules/{FineApprovals.Reopen}"), new ApprovalRuleRequest(false), Json)).StatusCode);
        Assert.True((await admin.PutAsJsonAsync(U($"/api/approval-rules/{FineApprovals.Reopen}"), new ApprovalRuleRequest(false), Json)).IsSuccessStatusCode);
        Assert.Equal("APPROVAL_THRESHOLD_INVALID", (await Error(await admin.PutAsJsonAsync(U($"/api/approval-rules/{FineApprovals.Discount}"), new ApprovalRuleRequest(true, Percent: 150), Json))).Code);
        var reopened = await Put(cashier, paid.Ticket, Save(paid.Ticket, status: FineTicketStatus.Open, reason: "Nhập sai số tiền"));
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
    }

    [Fact]
    public async Task Waiving_by_lowering_the_manual_total_or_deleting_a_line_is_judged_like_a_discount()
    {
        var (_, admin, cashier, ticket) = await Setup("KS-06");

        // Hạ số tiền phiếu thủ công về 0: cần lý do, rồi vượt ngưỡng → chờ duyệt (không lách bằng đường khác ngoài ô giảm trừ).
        var zero = new SaveFineTicketRequest(ticket.Status, 0, 0, ticket.Note, ManualAmount: 0);
        Assert.Equal("REASON_REQUIRED", (await Error(await Put(cashier, ticket, zero))).Code);
        Assert.Equal(HttpStatusCode.Accepted, (await Put(cashier, ticket, zero with { Reason = "Xin miễn" })).StatusCode);
        Assert.Equal(100_000m, (await Read<FineTicketDetail>(await admin.GetAsync(U($"/api/fine-tickets/Detail/{ticket.PublicId}")))).Ticket.Total);

        // Phiếu có 2 dòng 60k + 40k: xoá dòng 40k (40% tổng) → chờ duyệt, dòng vẫn còn.
        await admin.PostAsync(U("/api/fine-reasons/AddDefaults"), null);
        var two = await Read<FineSaveDetail>(await Put(admin, ticket, Save(ticket) with
        {
            ManualAmount = null, Lines = [new FineLineChange(null, "HUHONG", 60_000), new FineLineChange(null, "XLKY", 40_000)],
        }));
        var forty = two.Lines.Single(l => l.Amount == 40_000);
        Assert.Equal(HttpStatusCode.Accepted, (await Put(cashier, two.Ticket, Save(two.Ticket, reason: "Nhập thừa") with { DeletedLineIds = [forty.Id] })).StatusCode);
        Assert.Equal(2, (await Read<FineTicketDetail>(await admin.GetAsync(U($"/api/fine-tickets/Detail/{ticket.PublicId}")))).Lines.Count);

        // Tiền thu thêm không được gói chung vào yêu cầu chờ duyệt (người thu phải là người ghi nhận).
        var mixed = await Put(cashier, two.Ticket, Save(two.Ticket, discount: 60_000, paid: 10_000, reason: "Miễn một phần"));
        Assert.Equal("FINE_PAY_WITH_APPROVAL", (await Error(mixed)).Code);
    }

    [Fact]
    public async Task Parallel_cash_saves_never_leave_the_ledger_out_of_step_with_the_paid_total_and_paid_tickets_cannot_be_deleted()
    {
        var (_, admin, _, ticket) = await Setup("KS-07");

        var results = await Task.WhenAll(Put(admin, ticket, Save(ticket, paid: 40_000)), Put(admin, ticket, Save(ticket, paid: 60_000)));
        Assert.All(results, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict or HttpStatusCode.BadRequest,  r.StatusCode.ToString()));
        var after = await Read<FineTicketDetail>(await admin.GetAsync(U($"/api/fine-tickets/Detail/{ticket.PublicId}")));
        Assert.Equal(after.Ticket.Paid, after.Payments!.Sum(p => p.Amount)); // bất biến: Paid = tổng sổ thanh toán

        // Đã thu rồi hoàn hết (Paid = 0) vẫn không xoá được: sổ thanh toán phải còn.
        var refund = await admin.PostAsJsonAsync(U($"/api/fine-tickets/Adjust/{ticket.PublicId}"), new AdjustFinePaymentRequest(-after.Ticket.Paid, "Hoàn toàn bộ"), Json);
        Assert.True(refund.IsSuccessStatusCode);
        var delete = new HttpRequestMessage(HttpMethod.Delete, U($"/api/fine-tickets/Delete/{ticket.PublicId}"));
        delete.Headers.Add(CrudDiff.ReasonHeader, "Lập nhầm");
        Assert.Equal("FINE_TICKET_LOCKED", (await Error(await admin.SendAsync(delete))).Code);
    }

    [Fact]
    public async Task Every_change_of_what_is_owed_is_announced_to_the_payment_service_and_a_qr_payment_closes_the_ticket_once()
    {
        var (tenantId, admin, _, ticket) = await Setup("KS-08");
        ChargeIssued Last() => Factory.PublishedOf<ChargeIssued>().Where(e => e.SourceId == ticket.PublicId).MaxBy(e => e.Version)!;
        Assert.Equal((100_000m, "circulation", "FINE_TICKET"), (Last().Amount, Last().SourceService, Last().Kind));
        Assert.Equal("Phiếu phạt PT001", Last().Label);

        await Put(admin, ticket, Save(ticket, paid: 40_000));
        Assert.Equal(60_000m, Last().Amount);
        var afterCash = await Read<FineTicketDetail>(await admin.GetAsync(U($"/api/fine-tickets/Detail/{ticket.PublicId}")));

        // Tiền về qua QR: một khoản QR trong sổ, phiếu hoàn thành; event gửi lại không ghi lần hai.
        var paid = new PaymentSucceeded
        {
            TenantId = tenantId, SourceService = "circulation", SourceId = ticket.PublicId, Kind = "FINE_TICKET", TransactionCode = "PT261022000000ABCDEF",
            Provider = "VietQR", Amount = 60_000, PaidAt = DateTimeOffset.UtcNow, ReaderPublicId = afterCash.Ticket.ReaderPublicId,
        };
        await Publish(paid);
        await Publish(paid with { EventId = Guid.CreateVersion7() });
        var done = await Read<FineTicketDetail>(await admin.GetAsync(U($"/api/fine-tickets/Detail/{ticket.PublicId}")));
        Assert.Equal((100_000m, 0m, FineTicketStatus.Done), (done.Ticket.Paid, done.Ticket.Remaining, done.Ticket.Status));
        Assert.Equal(["CASH", "QR"], done.Payments!.Select(p => p.Method).ToArray());
        Assert.Equal(0m, Last().Amount); // không còn gì phải thu
    }

    [Fact]
    public async Task Cash_and_qr_paying_the_same_ticket_keep_both_in_the_ledger_and_the_excess_becomes_an_approvable_refund_proposal()
    {
        var (tenantId, admin, _, ticket) = await Setup("KS-10");
        var cash = await Read<FineSaveDetail>(await Put(admin, ticket, Save(ticket, paid: 100_000, status: FineTicketStatus.Done))); // quầy thu đủ tiền mặt

        // Tiền QR về sau đó (event ChargeSettled/ChargeIssued 0 chưa kịp tới payment lúc IPN): ghi đủ số thực nhận, phiếu âm.
        var paid = new PaymentSucceeded
        {
            TenantId = tenantId, SourceService = "circulation", SourceId = ticket.PublicId, Kind = "FINE_TICKET", TransactionCode = "PT261022000000FFEEDD",
            Provider = "VNPAY", Amount = 100_000, PaidAt = DateTimeOffset.UtcNow, ReaderPublicId = cash.Ticket.ReaderPublicId,
        };
        await Publish(paid);
        var detail = await Read<FineTicketDetail>(await admin.GetAsync(U($"/api/fine-tickets/Detail/{ticket.PublicId}")));
        Assert.Equal((200_000m, -100_000m), (detail.Ticket.Paid, detail.Ticket.Remaining));
        Assert.Equal("PT261022000000FFEEDD", Factory.PublishedOf<ChargeIssued>().Where(e => e.SourceId == ticket.PublicId).MaxBy(e => e.Version)!.LastPaymentCode);

        // Có đề xuất hoàn phần dư trong hộp duyệt — không tự hoàn tiền.
        var inbox = await Read<CrudPage<ChangeRequestDto>>(await admin.PostAsJsonAsync(U("/api/approvals/Search"), new ChangeRequestSearch { Scope = "inbox" }, Json));
        var proposal = Assert.Single(inbox.Items, r => r.TargetPublicId == ticket.PublicId);
        Assert.Equal((FineApprovals.Adjust, 100_000m, true), (proposal.Kind, proposal.Amount, proposal.CanDecide)); // người duyệt quyết định, kể cả quản trị duy nhất

        await Read<ChangeRequestDto>(await admin.PostAsJsonAsync(U($"/api/approvals/{proposal.PublicId}/Approve"), new ApprovalDecisionRequest("Hoàn cho bạn đọc"), Json));
        var after = await Read<FineTicketDetail>(await admin.GetAsync(U($"/api/fine-tickets/Detail/{ticket.PublicId}")));
        Assert.Equal((100_000m, 0m), (after.Ticket.Paid, after.Ticket.Remaining));
        Assert.Equal(["ADJUST", "CASH", "QR"], after.Payments!.Select(p => p.Method).Order().ToArray());
    }

    [Fact]
    public async Task Deleting_an_unpaid_ticket_withdraws_the_charge()
    {
        var (_, admin, _, ticket) = await Setup("KS-09");
        var delete = new HttpRequestMessage(HttpMethod.Delete, U($"/api/fine-tickets/Delete/{ticket.PublicId}"));
        delete.Headers.Add(CrudDiff.ReasonHeader, "Lập nhầm");
        Assert.True((await admin.SendAsync(delete)).IsSuccessStatusCode);
        Assert.Contains(Factory.PublishedOf<ChargeWaived>(), e => e.SourceId == ticket.PublicId);
    }

    /// <summary>Bản đọc được của kết quả lưu (200): chi tiết phiếu kèm sổ thanh toán.</summary>
    private sealed record FineSaveDetail(FineTicketDto Ticket, IReadOnlyList<FineLineDto> Lines, IReadOnlyList<FinePaymentDto>? Payments);
}
