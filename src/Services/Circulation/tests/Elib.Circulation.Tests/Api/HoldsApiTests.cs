using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.Circulation.Application;
using Elib.Circulation.Domain;
using Elib.Contracts.Events.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Circulation.Tests.Api;

public sealed class HoldsApiTests(CirculationApiFactory factory) : CirculationTestBase(factory)
{
    private static Task<HttpResponseMessage> Place(HttpClient staff, string card, long? mfn = null, string? barcode = null) =>
        staff.PostAsJsonAsync(U("/api/holds/Place"), new PlaceHoldRequest(card, mfn, barcode), Json);

    private async Task<CirculationJobResult> RunJobs(long tenantId)
    {
        using var scope = Factory.Services.CreateScope();
        using var use = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
        return await scope.ServiceProvider.GetRequiredService<CirculationJobs>().RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Returned_copy_is_held_for_the_first_waiting_reader_and_only_they_can_borrow_it()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "H1");
        var mfn = NextMfn();
        await Publish(Reader(tenantId, "DM-A"), Reader(tenantId, "DM-B", email: "b@example.com"), Reader(tenantId, "DM-C"),
            Bib(tenantId, mfn, "Hoàng tử bé"), Item(tenantId, "HT001", mfn));
        Assert.Equal(1, (await Read<CheckoutResult>(await Checkout(staff, "DM-A", place.Id, "HT001"))).Succeeded);

        var b = await Read<HoldDto>(await Place(staff, "dm-b", mfn: mfn));
        Assert.Equal((HoldStatus.Waiting, (int?)1, "Hoàng tử bé"), (b.Status, b.QueuePosition, b.Title));
        var c = await Read<HoldDto>(await Place(staff, "DM-C", barcode: "ht001"));
        Assert.Equal((mfn, (int?)2), (c.Mfn, c.QueuePosition));
        Assert.Equal("HOLD_EXISTS", (await Error(await Place(staff, "DM-B", mfn: mfn))).Code);
        Assert.Equal("HOLD_ALREADY_BORROWED", (await Error(await Place(staff, "DM-A", mfn: mfn))).Code);

        // Trả → bản giữ cho người đặt đầu tiên, báo bạn đọc qua notification.
        var returned = await Read<ReturnResult>(await staff.PostAsJsonAsync(U("/api/loans/Return"), new ReturnRequest("HT001"), Json));
        Assert.Equal(("DM-B", HoldStatus.Ready, "HT001"), (returned.HoldFor!.CardNo, returned.HoldFor.Status, returned.HoldFor.Barcode));
        var ready = Factory.PublishedOf<NotificationRequested>().Single(n => n.TenantId == tenantId && n.TemplateCode == CirculationTemplates.HoldReady);
        Assert.Equal(("b@example.com", "HT001", "Hoàng tử bé"), (ready.Recipient.Email, ready.Data["barcode"], ready.Data["title"]));

        var refused = await Read<CheckoutResult>(await Checkout(staff, "DM-C", place.Id, "HT001"));
        Assert.Contains("đang giữ cho bạn đọc đặt mượn (thẻ DM-B)", Assert.Single(refused.Lines).Message, StringComparison.Ordinal);

        var panel = await Read<ReaderPanel>(await staff.PostAsJsonAsync(U("/api/loans/Reader"), new ReaderPanelRequest("DM-B"), Json));
        Assert.Equal(("HT001", HoldStatus.Ready), (Assert.Single(panel.Holds!).Barcode, panel.Holds![0].Status));
        Assert.Equal(1, (await Read<CheckoutResult>(await Checkout(staff, "DM-B", place.Id, "HT001"))).Succeeded);

        var all = await Read<CrudPage<HoldDto>>(await staff.PostAsJsonAsync(U("/api/holds/Search"), new HoldSearch { Mfn = mfn }, Json));
        Assert.Equal([("DM-C", HoldStatus.Waiting, (int?)1), ("DM-B", HoldStatus.Fulfilled, null)], all.Items.Select(h => (h.CardNo, h.Status, h.QueuePosition)));

        var cancelled = await Read<HoldDto>(await staff.PostAsJsonAsync(U("/api/holds/Cancel"), new CancelHoldRequest(c.PublicId, "Không cần nữa"), Json));
        Assert.Equal(HoldStatus.Cancelled, cancelled.Status);
        Assert.Equal("HOLD_CLOSED", (await Error(await staff.PostAsJsonAsync(U("/api/holds/Cancel"), new CancelHoldRequest(c.PublicId), Json))).Code);
    }

    [Fact]
    public async Task Jobs_remind_due_and_overdue_once_and_pass_expired_holds_to_the_next_reader()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "H2");
        await Read<LoanPolicyDto>(await staff.PostAsJsonAsync(U("/api/loan-policies/Add"), new LoanPolicyRequest(null, null, 14, HoldDays: 2, MaxHolds: 1), Json));
        var (loanMfn, holdMfn, otherMfn) = (NextMfn(), NextMfn(), NextMfn());
        await Publish(Reader(tenantId, "NH-D", email: "d@example.com"), Reader(tenantId, "NH-F", email: "f@example.com"), Reader(tenantId, "NH-G", email: "g@example.com"),
            Bib(tenantId, loanMfn, "Nhà giả kim"), Item(tenantId, "NG001", loanMfn), Bib(tenantId, holdMfn, "Chí Phèo"), Item(tenantId, "CP001", holdMfn),
            Item(tenantId, "KH001", otherMfn));
        Assert.Equal(1, (await Read<CheckoutResult>(await Checkout(staff, "NH-D", place.Id, "NG001"))).Succeeded);

        var f = await Read<HoldDto>(await Place(staff, "NH-F", mfn: holdMfn)); // còn bản → giữ ngay
        Assert.Equal((HoldStatus.Ready, "CP001"), (f.Status, f.Barcode));
        Assert.Equal("HOLD_LIMIT", (await Error(await Place(staff, "NH-F", mfn: otherMfn))).Code);
        var g = await Read<HoldDto>(await Place(staff, "NH-G", mfn: holdMfn));
        Assert.Equal(HoldStatus.Waiting, g.Status);
        Assert.Equal(new CirculationJobResult(0, 0, 0), await RunJobs(tenantId));

        // Ngày 12: hạn trả (ngày 14) còn 2 ngày → nhắc một lần; giữ chỗ của F (2 ngày) đã hết → chuyển bản cho G.
        Factory.Clock.Advance(TimeSpan.FromDays(12));
        Assert.Equal(new CirculationJobResult(1, 0, 1), await RunJobs(tenantId));
        Assert.Equal(new CirculationJobResult(0, 0, 0), await RunJobs(tenantId));
        var notices = Factory.PublishedOf<NotificationRequested>().Where(n => n.TenantId == tenantId).ToList();
        Assert.Equal("NG001", notices.Single(n => n.TemplateCode == CirculationTemplates.DueSoon).Data["barcode"]);
        Assert.Equal("f@example.com", notices.Single(n => n.TemplateCode == CirculationTemplates.HoldExpired).Recipient.Email);
        Assert.Equal("g@example.com", notices.Single(n => n.TemplateCode == CirculationTemplates.HoldReady && n.Recipient.Email == "g@example.com").Recipient.Email);
        var holds = await Read<CrudPage<HoldDto>>(await staff.PostAsJsonAsync(U("/api/holds/Search"), new HoldSearch { Mfn = holdMfn }, Json));
        Assert.Equal([("NH-G", HoldStatus.Ready), ("NH-F", HoldStatus.Expired)], holds.Items.Select(h => (h.CardNo, h.Status)));

        // Ngày 15: quá hạn 1 ngày → báo quá hạn một lần.
        Factory.Clock.Advance(TimeSpan.FromDays(3));
        Assert.Equal(1, (await RunJobs(tenantId)).Overdue);
        Assert.Equal(0, (await RunJobs(tenantId)).Overdue);
    }
}
