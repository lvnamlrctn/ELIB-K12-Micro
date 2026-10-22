using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elib.BuildingBlocks.Crud;
using Elib.Circulation.Application;
using Elib.Circulation.Domain;
using Elib.Contracts.Events.Circulation;
using Elib.Contracts.Events.Holdings;
using Elib.Contracts.Events.Platform;

namespace Elib.Circulation.Tests.Api;

/// <summary>Đóng cưỡng bức, mở lại lượt mượn và đóng lượt vì mất: lý do, phê duyệt theo thẩm quyền, hoàn tác có ràng buộc.</summary>
public sealed class LoanControlsApiTests(CirculationApiFactory factory) : CirculationTestBase(factory)
{
    private static readonly string[] CounterGrants = ["BORROW:view", "BORROW:add", "BORROW:edit", "FINES:view", "FINES:add", "FINES:edit"];

    private async Task<(long TenantId, HttpClient Admin, HttpClient Counter, CircPlaceDto Place)> Setup(string card, params string[] barcodes)
    {
        var tenantId = NewTenant();
        var admin = Staff(tenantId);
        var counter = Staff(tenantId, CounterGrants);
        var place = await AddPlace(admin, "Q" + card);
        var mfn = NextMfn();
        var events = new List<Elib.Contracts.Events.IntegrationEvent> { Reader(tenantId, card), Reader(tenantId, card + "B") };
        events.AddRange(barcodes.Select(b => (Elib.Contracts.Events.IntegrationEvent)Item(tenantId, b, mfn)));
        await Publish([.. events]);
        return (tenantId, admin, counter, place);
    }

    private static async Task<LoanDto> Borrow(HttpClient c, string card, long place, string barcode) =>
        (await Read<CheckoutResult>(await Checkout(c, card, place, barcode))).Lines[0].Loan!;

    private static long UserIdOf(HttpClient client) =>
        long.Parse(client.DefaultRequestHeaders.GetValues(Elib.BuildingBlocks.Testing.HeaderAuthHandler.HeaderName).Single().Split(';')
            .Single(p => p.StartsWith("sub=", StringComparison.Ordinal))[4..], System.Globalization.CultureInfo.InvariantCulture);

    private static Task<HttpResponseMessage> Act(HttpClient c, string action, Guid loanId, string reason) =>
        c.PostAsJsonAsync(U($"/api/loans/{action}"), new LoanActionRequest(loanId, reason), Json);

    [Fact]
    public async Task Force_closing_needs_a_reason_waits_for_approval_and_leaves_the_item_available()
    {
        var (tenantId, admin, counter, place) = await Setup("DC-01", "DC001");
        var loan = await Borrow(counter, "DC-01", place.Id, "DC001");

        Assert.Equal("REASON_REQUIRED", (await Error(await Act(counter, "ForceClose", loan.PublicId, " "))).Code);
        var held = await Act(counter, "ForceClose", loan.PublicId, "Bạn đọc đã chuyển trường, không liên lạc được");
        Assert.Equal(HttpStatusCode.Accepted, held.StatusCode);
        var pending = await Read<PendingApproval>(held);
        Assert.Equal(LoanApprovals.ForceClose, pending.Kind);
        Assert.Null((await Read<LoanDto>(await admin.GetAsync(U($"/api/loans/GetById/{loan.PublicId}")))).ReturnedAt); // chưa đổi gì

        Assert.Equal("APPROVAL_FORBIDDEN", (await Error(await counter.PostAsJsonAsync(U($"/api/approvals/{pending.RequestId}/Approve"), new ApprovalDecisionRequest(), Json))).Code);
        var approved = await Read<ChangeRequestDto>(await admin.PostAsJsonAsync(U($"/api/approvals/{pending.RequestId}/Approve"), new ApprovalDecisionRequest("Đã xác minh"), Json));
        Assert.Equal(ApprovalStatus.Approved, approved.Status);

        var closed = await Read<LoanDto>(await admin.GetAsync(U($"/api/loans/GetById/{loan.PublicId}")));
        Assert.Equal((Loan.CloseForced, "Bạn đọc đã chuyển trường, không liên lạc được"), (closed.CloseKind, closed.CloseReason));
        Assert.NotNull(closed.ReturnedAt);
        var evt = Factory.PublishedOf<LoanChanged>().Last(e => e.LoanPublicId == loan.PublicId);
        Assert.Equal((Loan.CloseForced, null), (evt.CloseKind, evt.ClosedItemStatus)); // bản sách KHÔNG chuyển Mất
        var audit = Factory.PublishedOf<AuditRecorded>().Single(a => a.TenantId == tenantId && a.EntityType == "Loan" && a.Action == "UPDATE" && a.EntityId == loan.PublicId.ToString());
        using var json = JsonDocument.Parse(audit.ChangesJson!);
        Assert.Equal("Forced", json.RootElement.GetProperty("closeKind")[1].GetString());
        Assert.Equal("Bạn đọc đã chuyển trường, không liên lạc được", json.RootElement.GetProperty("_reason").GetString());

        // Người có quyền duyệt đóng thẳng, không qua yêu cầu.
        var loan2 = await Borrow(admin, "DC-01", place.Id, "DC001");
        Assert.Equal(HttpStatusCode.OK, (await Act(admin, "ForceClose", loan2.PublicId, "Dọn dữ liệu cuối năm")).StatusCode);
    }

    [Fact]
    public async Task Reopening_a_closed_loan_restores_it_unless_the_item_moved_on_or_a_paid_fine_depends_on_it()
    {
        var (_, admin, counter, place) = await Setup("DC-02", "DC002", "DC003");
        var loan = await Borrow(admin, "DC-02", place.Id, "DC002");
        Assert.Equal("LOAN_NOT_CLOSED", (await Error(await Act(admin, "Reopen", loan.PublicId, "Nhầm"))).Code);
        Assert.True((await admin.PostAsJsonAsync(U("/api/loans/Return"), new ReturnRequest(Barcode: "DC002"), Json)).IsSuccessStatusCode);

        // Người thu ngân (không có quyền duyệt) chỉ gửi được yêu cầu.
        Assert.Equal(HttpStatusCode.Accepted, (await Act(counter, "Reopen", loan.PublicId, "Quét nhầm mã trả")).StatusCode);

        var reopened = await Read<LoanDto>(await Act(admin, "Reopen", loan.PublicId, "Quét nhầm mã trả"));
        Assert.Null(reopened.ReturnedAt);
        var evt = Factory.PublishedOf<LoanChanged>().Last(e => e.LoanPublicId == loan.PublicId);
        Assert.True(evt.Reopened);
        Assert.Null(evt.ReturnedAt);

        // Trả lại rồi cho bạn đọc khác mượn: lượt cũ không mở lại được nữa (bản đã đi tiếp).
        Assert.True((await admin.PostAsJsonAsync(U("/api/loans/Return"), new ReturnRequest(Barcode: "DC002"), Json)).IsSuccessStatusCode);
        await Borrow(admin, "DC-02B", place.Id, "DC002");
        Assert.Equal("LOAN_ITEM_REBORROWED", (await Error(await Act(admin, "Reopen", loan.PublicId, "Lại nhầm"))).Code);
    }

    [Fact]
    public async Task A_lost_line_that_closes_a_loan_needs_approval_and_the_closed_loan_cannot_be_reopened_while_the_line_stands()
    {
        var (_, admin, counter, place) = await Setup("DC-04", "DC004");
        var loan = await Borrow(admin, "DC-04", place.Id, "DC004");

        var built = await Read<FineTicketDetail>(await counter.PostAsJsonAsync(U("/api/fine-tickets/BuildForReader"), new BuildFineTicketRequest("DC-04", LoanIds: [loan.PublicId]), Json));
        var lostLine = Assert.Single(built.Lines);
        Assert.Equal("MATTL", lostLine.ReasonCode);

        // Không lý do → báo thiếu; có lý do nhưng không có BORROW:approve → chờ duyệt, lượt mượn vẫn mở.
        var save = new SaveFineTicketRequest(FineTicketStatus.Open, 0, 0, Lines: [new FineLineChange(lostLine.Id, "MATTL", lostLine.Amount)]);
        Assert.Equal("REASON_REQUIRED", (await Error(await counter.PutAsJsonAsync(U($"/api/fine-tickets/Save/{built.Ticket.PublicId}"), save, Json))).Code);
        var held = await counter.PutAsJsonAsync(U($"/api/fine-tickets/Save/{built.Ticket.PublicId}"), save with { Reason = "Bạn đọc báo làm mất sách" }, Json);
        Assert.Equal(HttpStatusCode.Accepted, held.StatusCode);
        var pending = await Read<PendingApproval>(held);
        Assert.Equal(LoanApprovals.CloseLost, pending.Kind);
        Assert.Null((await Read<LoanDto>(await admin.GetAsync(U($"/api/loans/GetById/{loan.PublicId}")))).ReturnedAt);

        await Read<ChangeRequestDto>(await admin.PostAsJsonAsync(U($"/api/approvals/{pending.RequestId}/Approve"), new ApprovalDecisionRequest(), Json));
        var closed = await Read<LoanDto>(await admin.GetAsync(U($"/api/loans/GetById/{loan.PublicId}")));
        Assert.Equal((Loan.CloseLost, "Bạn đọc báo làm mất sách"), (closed.CloseKind, closed.CloseReason));
        Assert.Equal("L", Factory.PublishedOf<LoanChanged>().Last(e => e.LoanPublicId == loan.PublicId).ClosedItemStatus);

        // Dòng "Mất tài liệu" còn trên phiếu → không mở lại lượt (tiền và trạng thái bản sách phải đi cùng nhau).
        Assert.Equal("LOAN_REOPEN_LOST_LINE", (await Error(await Act(admin, "Reopen", loan.PublicId, "Đóng nhầm"))).Code);
    }

    [Fact]
    public async Task Finding_a_lost_item_again_proposes_removing_the_fine_line_and_approval_refunds_what_was_paid()
    {
        var (tenantId, admin, counter, place) = await Setup("DC-05", "DC005");
        var loan = await Borrow(admin, "DC-05", place.Id, "DC005");
        var built = await Read<FineTicketDetail>(await admin.PostAsJsonAsync(U("/api/fine-tickets/BuildForReader"), new BuildFineTicketRequest("DC-05", LoanIds: [loan.PublicId]), Json));
        var lostLine = Assert.Single(built.Lines);
        var done = await Read<FineSaveDetail>(await admin.PutAsJsonAsync(U($"/api/fine-tickets/Save/{built.Ticket.PublicId}"),
            new SaveFineTicketRequest(FineTicketStatus.Done, 0, 50_000, Lines: [new FineLineChange(lostLine.Id, "MATTL", 50_000)], Reason: "Mất sách, đền tiền"), Json));
        Assert.Equal((50_000m, 0m), (done.Ticket.Paid, done.Ticket.Remaining));

        var found = new ItemLossReversed
        {
            TenantId = tenantId, ItemPublicId = loan.ItemPublicId, Barcode = "DC005", CardNo = "DC-05", Reason = "Tìm thấy ở góc kho", ActorName = "Thủ kho (thukho)",
            Actor = new Elib.Contracts.Events.EventActor(UserIdOf(admin), "staff"), // người khôi phục chính là quản trị duy nhất: vẫn phải duyệt được
        };
        await Publish(found);
        await Publish(found with { EventId = Guid.CreateVersion7() }); // gửi trùng: không lập thêm yêu cầu

        var inbox = await Read<CrudPage<ChangeRequestDto>>(await admin.PostAsJsonAsync(U("/api/approvals/Search"), new ChangeRequestSearch { Scope = "inbox" }, Json));
        var request = Assert.Single(inbox.Items);
        Assert.Equal((FineApprovals.LostRefund, "Thủ kho (thukho)", 50_000m), (request.Kind, request.RequestedByName, request.Amount));
        Assert.Single((await Read<FineTicketDetail>(await admin.GetAsync(U($"/api/fine-tickets/Detail/{built.Ticket.PublicId}")))).Lines); // chưa tự đổi gì
        Assert.Empty((await Read<CrudPage<ChangeRequestDto>>(await counter.PostAsJsonAsync(U("/api/approvals/Search"), new ChangeRequestSearch { Scope = "inbox" }, Json))).Items);

        await Read<ChangeRequestDto>(await admin.PostAsJsonAsync(U($"/api/approvals/{request.PublicId}/Approve"), new ApprovalDecisionRequest("Đồng ý hoàn"), Json));
        var after = await Read<FineTicketDetail>(await admin.GetAsync(U($"/api/fine-tickets/Detail/{built.Ticket.PublicId}")));
        Assert.Empty(after.Lines);
        Assert.Equal((0m, 0m, FineTicketStatus.Done), (after.Ticket.Paid, after.Ticket.Remaining, after.Ticket.Status));
        Assert.Equal([50_000m, -50_000m], after.Payments!.Select(p => p.Amount).ToArray()); // sổ giữ nguyên khoản thu, thêm khoản hoàn
        // Lượt mượn vẫn đóng (không tự mở lại).
        Assert.NotNull((await Read<LoanDto>(await admin.GetAsync(U($"/api/loans/GetById/{loan.PublicId}")))).ReturnedAt);
    }

    [Fact]
    public async Task Two_approvers_pressing_approve_at_once_apply_the_request_only_once()
    {
        var (tenantId, admin, counter, place) = await Setup("DC-06", "DC006");
        var admin2 = Staff(tenantId);
        var loan = await Borrow(counter, "DC-06", place.Id, "DC006");
        var pending = await Read<PendingApproval>(await Act(counter, "ForceClose", loan.PublicId, "Dọn dữ liệu"));

        var results = await Task.WhenAll(
            admin.PostAsJsonAsync(U($"/api/approvals/{pending.RequestId}/Approve"), new ApprovalDecisionRequest(), Json),
            admin2.PostAsJsonAsync(U($"/api/approvals/{pending.RequestId}/Approve"), new ApprovalDecisionRequest(), Json));
        Assert.Single(results, r => r.IsSuccessStatusCode);
        Assert.Equal(1, (await Read<LoanDto>(await admin.GetAsync(U($"/api/loans/GetById/{loan.PublicId}")))).Version); // đóng đúng một lần (mượn → đóng)
    }

    private sealed record FineSaveDetail(FineTicketDto Ticket, IReadOnlyList<FineLineDto> Lines, IReadOnlyList<FinePaymentDto>? Payments);
}
