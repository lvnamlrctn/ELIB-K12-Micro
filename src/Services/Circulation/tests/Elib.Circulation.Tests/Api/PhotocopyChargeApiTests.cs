using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.Circulation.Application;
using Elib.Contracts.Events.Payment;

namespace Elib.Circulation.Tests.Api;

/// <summary>Phiếu sao chụp là nguồn khoản thu thứ hai của dịch vụ thanh toán: phát khoản thu, nhận tiền về, không ghi hai lần.</summary>
public sealed class PhotocopyChargeApiTests(CirculationApiFactory factory) : CirculationTestBase(factory)
{
    [Fact]
    public async Task A_photocopy_announces_what_is_owed_and_a_qr_payment_marks_it_paid_once()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var mfn = NextMfn();
        await Publish(Reader(tenantId, "PH-11"), Bib(tenantId, mfn, "Giáo trình Lý"), Item(tenantId, "GL001", mfn));
        var photo = await Read<PhotocopyDto>(await staff.PostAsJsonAsync(U("/api/photocopies/Add"), new PhotocopyRequest("PH-11", "GL001", 1, 10, 2, 500), Json));

        ChargeIssued Last() => Factory.PublishedOf<ChargeIssued>().Where(e => e.SourceId == photo.PublicId).MaxBy(e => e.Version)!;
        Assert.Equal(("PHOTOCOPY", 10_000m, "PH-11"), (Last().Kind, Last().Amount, Last().CardNo));

        var paid = new PaymentSucceeded
        {
            TenantId = tenantId, SourceService = "circulation", SourceId = photo.PublicId, Kind = "PHOTOCOPY", TransactionCode = "PT261022000000A1B2C3",
            Provider = "VietQR", Amount = 10_000, PaidAt = DateTimeOffset.UtcNow, ReaderPublicId = photo.ReaderPublicId,
        };
        await Publish(paid);
        await Publish(paid with { EventId = Guid.CreateVersion7() }); // gửi lại: không ghi hai lần
        Assert.True((await Read<PhotocopyDto>(await staff.GetAsync(U($"/api/photocopies/GetById/{photo.PublicId}")))).Paid);
        Assert.Equal((0m, "PT261022000000A1B2C3"), (Last().Amount, Last().LastPaymentCode));

        // Thu nhầm / hoàn: bỏ đánh dấu đã thu → nợ trở lại, mã đã ghi nhận được gửi kèm để payment cho phép mở lại.
        await Read<PhotocopyDto>(await staff.PostAsJsonAsync(U("/api/photocopies/SetPaid"), new PhotocopyPaidRequest(photo.PublicId, false), Json));
        Assert.Equal((10_000m, "PT261022000000A1B2C3"), (Last().Amount, Last().LastPaymentCode));

        Assert.True((await staff.DeleteAsync(U($"/api/photocopies/Delete/{photo.PublicId}"))).IsSuccessStatusCode);
        Assert.Contains(Factory.PublishedOf<ChargeWaived>(), e => e.SourceId == photo.PublicId);
    }

    [Fact]
    public async Task Unpaid_photocopies_are_offered_to_payment_as_existing_charges()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var mfn = NextMfn();
        await Publish(Reader(tenantId, "PH-12"), Bib(tenantId, mfn, "Giáo trình Hoá"), Item(tenantId, "GH001", mfn));
        var open = await Read<PhotocopyDto>(await staff.PostAsJsonAsync(U("/api/photocopies/Add"), new PhotocopyRequest("PH-12", "GH001", 1, 4, 1, 500), Json));
        await Read<PhotocopyDto>(await staff.PostAsJsonAsync(U("/api/photocopies/Add"), new PhotocopyRequest("PH-12", "GH001", 5, 5, 1, 500, Paid: true), Json));

        var service = Factory.CreateClient().WithClaims(TestClaims.Service);
        var page = await Read<Elib.Contracts.Events.Platform.StatePage<ChargeIssued>>(await service.GetAsync(U($"/internal/tenants/{tenantId}/charges/open?kind=PHOTOCOPY")));
        var charge = Assert.Single(page.Items);
        Assert.Equal((open.PublicId, 2_000m), (charge.SourceId, charge.Amount)); // chỉ phiếu chưa thu
    }
}
