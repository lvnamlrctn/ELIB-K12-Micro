using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.Circulation.Application;
using Elib.Circulation.Domain;
using Elib.Contracts.Events.Circulation;

namespace Elib.Circulation.Tests.Api;

public sealed class FinesApiTests(CirculationApiFactory factory) : CirculationTestBase(factory)
{
    [Fact]
    public async Task Overdue_loans_are_gathered_into_one_ticket_and_a_lost_line_closes_the_loan()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "P1");
        await Read<LoanPolicyDto>(await staff.PostAsJsonAsync(U("/api/loan-policies/Add"), new LoanPolicyRequest(null, null, 14, FinePerDay: 2000), Json));
        var mfn = NextMfn();
        await Publish(Reader(tenantId, "PH-01"), Bib(tenantId, mfn, "Đất rừng phương Nam"), Item(tenantId, "PH001", mfn), Item(tenantId, "PH002", mfn));
        Assert.Equal(2, (await Read<CheckoutResult>(await Checkout(staff, "PH-01", place.Id, "PH001", "PH002"))).Succeeded);

        Factory.Clock.Advance(TimeSpan.FromDays(20)); // hạn 14 ngày → quá hạn 6 ngày
        var built = await Read<FineTicketDetail>(await staff.PostAsJsonAsync(U("/api/fine-tickets/BuildForReader"), new BuildFineTicketRequest("ph-01"), Json));
        Assert.Equal(("PT001", 1, 24000m, 2000m), (built.Ticket.Code, built.Ticket.Round, built.Ticket.Total, built.FinePerDay));
        Assert.All(built.Lines, l => Assert.Equal(("QUAHAN", "Quá hạn", 6, 12000m, "Đất rừng phương Nam", true), (l.ReasonCode, l.ReasonName, l.OverdueDays, l.Amount, l.Title, l.LoanOpen)));

        // Gom lại: không thêm dòng trùng, vẫn phiếu cũ.
        var again = await Read<FineTicketDetail>(await staff.PostAsJsonAsync(U("/api/fine-tickets/BuildForReader"), new BuildFineTicketRequest("PH-01"), Json));
        Assert.Equal((built.Ticket.PublicId, 2), (again.Ticket.PublicId, again.Lines.Count));

        // Dòng 2 đổi sang "Mất tài liệu": đóng lượt mượn, báo holdings bản sách mất.
        var lostLine = built.Lines.Single(l => l.Barcode == "PH002");
        var keptLine = built.Lines.Single(l => l.Barcode == "PH001");
        var saved = await Read<FineTicketDetail>(await staff.PutAsJsonAsync(U($"/api/fine-tickets/Save/{built.Ticket.PublicId}"),
            new SaveFineTicketRequest(FineTicketStatus.Done, 2000, 60000, "Đền sách",
                Lines: [new FineLineChange(keptLine.Id, "QUAHAN", 12000), new FineLineChange(lostLine.Id, "MATTL", 50000)]), Json));
        Assert.Equal((62000m, 2000m, 60000m, 0m, 2), (saved.Ticket.Total, saved.Ticket.Discount, saved.Ticket.Paid, saved.Ticket.Remaining, saved.Ticket.Status));
        Assert.False(saved.Lines.Single(l => l.Barcode == "PH002").LoanOpen);
        var lost = Factory.PublishedOf<LoanChanged>().Single(e => e.Barcode == "PH002" && e.ReturnedAt is not null);
        Assert.Equal("L", lost.ClosedItemStatus);

        var panel = await Read<ReaderPanel>(await staff.PostAsJsonAsync(U("/api/loans/Reader"), new ReaderPanelRequest("PH-01"), Json));
        Assert.Equal(("PH001", 0m), (Assert.Single(panel.CurrentLoans).Barcode, panel.UnpaidFines));

        // Phiếu đã hoàn thành: không thêm dòng, không xoá được.
        var locked = await staff.PutAsJsonAsync(U($"/api/fine-tickets/Save/{built.Ticket.PublicId}"),
            new SaveFineTicketRequest(FineTicketStatus.Done, 0, 0, Lines: [new FineLineChange(null, "HUHONG", 10000)]), Json);
        Assert.Equal("FINE_TICKET_DONE", (await Error(locked)).Code);
        Assert.Equal("FINE_TICKET_LOCKED", (await Error(await staff.DeleteAsync(U($"/api/fine-tickets/Delete/{built.Ticket.PublicId}")))).Code);

        var totals = await Read<FineTicketTotals>(await staff.PostAsJsonAsync(U("/api/fine-tickets/Totals"), new FineTicketSearch { CardNo = "ph-01" }, Json));
        Assert.Equal((60000m, 60000m, 0m), (totals.Receivable, totals.Received, totals.Remaining));
        var page = await Read<CrudPage<FineTicketDto>>(await staff.PostAsJsonAsync(U("/api/fine-tickets/Search"), new FineTicketSearch { Keyword = "pt001" }, Json));
        Assert.Equal(("Bạn đọc PH-01", 2), (Assert.Single(page.Items).ReaderName, page.Items[0].LineCount));
    }

    [Fact]
    public async Task Manual_tickets_count_as_unpaid_and_numbers_are_not_reused()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        await Publish(Reader(tenantId, "TC-01"));

        var first = await Read<FineTicketDto>(await staff.PostAsJsonAsync(U("/api/fine-tickets/Add"), new FineTicketCreateRequest("tc-01", 30000, "Làm hỏng thẻ"), Json));
        Assert.Equal(("PT001", 30000m, 30000m), (first.Code, first.Total, first.Remaining));
        var panel = await Read<ReaderPanel>(await staff.PostAsJsonAsync(U("/api/loans/Reader"), new ReaderPanelRequest("TC-01"), Json));
        Assert.Equal(30000m, panel.UnpaidFines);

        Assert.True((await staff.DeleteAsync(U($"/api/fine-tickets/Delete/{first.PublicId}"))).IsSuccessStatusCode);
        var second = await Read<FineTicketDto>(await staff.PostAsJsonAsync(U("/api/fine-tickets/Add"), new FineTicketCreateRequest("TC-01", 5000), Json));
        Assert.Equal(("PT002", 2), (second.Code, second.Round));

        // Thêm dòng theo ĐKCB lạ → lỗi; khoản tự do → tổng tính theo dòng thay số tiền nhập tay.
        var bad = await staff.PutAsJsonAsync(U($"/api/fine-tickets/Save/{second.PublicId}"),
            new SaveFineTicketRequest(FineTicketStatus.Open, 0, 0, Lines: [new FineLineChange(null, "QUAHAN", 1000, "KHONGCO")]), Json);
        Assert.Equal("FINE_REASON_INVALID", (await Error(bad)).Code); // đơn vị chưa có lý do phạt nào

        Assert.Equal(3, (await Read<Dictionary<string, int>>(await staff.PostAsync(U("/api/fine-reasons/AddDefaults"), null)))["added"]);
        var reasons = await Read<List<FineReasonDto>>(await staff.PostAsJsonAsync(U("/api/fine-reasons/SearchAll"), new CrudSearch(), Json));
        var overdue = reasons.Single(r => r.Code == "QUAHAN");
        Assert.True(overdue.IsBuiltIn);
        Assert.Equal("L", reasons.Single(r => r.Code == "MATTL").ItemStatus);
        Assert.Equal("FINE_REASON_BUILT_IN", (await Error(await staff.DeleteAsync(U($"/api/fine-reasons/Delete/{overdue.PublicId}")))).Code);

        var unknownItem = await staff.PutAsJsonAsync(U($"/api/fine-tickets/Save/{second.PublicId}"),
            new SaveFineTicketRequest(FineTicketStatus.Open, 0, 0, Lines: [new FineLineChange(null, "HUHONG", 1000, "KHONGCO")]), Json);
        Assert.Equal("ITEM_NOT_FOUND", (await Error(unknownItem)).Code);
        var withLine = await Read<FineTicketDetail>(await staff.PutAsJsonAsync(U($"/api/fine-tickets/Save/{second.PublicId}"),
            new SaveFineTicketRequest(FineTicketStatus.Open, 0, 2000, Lines: [new FineLineChange(null, "HUHONG", 15000)]), Json));
        Assert.Equal((15000m, 13000m), (withLine.Ticket.Total, withLine.Ticket.Remaining));
    }

    [Fact]
    public async Task Renew_from_today_policy_counts_from_the_renewal_day()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "P3");
        await Read<LoanPolicyDto>(await staff.PostAsJsonAsync(U("/api/loan-policies/Add"), new LoanPolicyRequest(null, null, 14, RenewDays: 7, RenewFromToday: true), Json));
        var mfn = NextMfn();
        await Publish(Reader(tenantId, "RT-01"), Item(tenantId, "RT001", mfn));
        var loan = (await Read<CheckoutResult>(await Checkout(staff, "RT-01", place.Id, "RT001"))).Lines[0].Loan!;

        Factory.Clock.Advance(TimeSpan.FromDays(2));
        var renewed = await Read<LoanDto>(await staff.PostAsJsonAsync(U("/api/loans/Renew"), new RenewRequest(loan.PublicId, "Đọc thêm"), Json));
        Assert.Equal(9.0, (renewed.DueAt - loan.LoanedAt).TotalDays); // hôm nay (ngày 2) + 7, không phải hạn cũ (ngày 14) + 7
    }
}
