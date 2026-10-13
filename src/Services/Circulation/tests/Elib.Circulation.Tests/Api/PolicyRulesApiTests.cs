using System.Net.Http.Json;
using Elib.Circulation.Application;
using Elib.Circulation.Domain;
using Elib.Circulation.Infrastructure;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Circulation.Tests.Api;

/// <summary>Luật theo nhóm tài liệu (ELIB-LRC: PolicyCircDocGroup — ở đây áp thật) và phí xử lý kỹ thuật/vận chuyển khi nợ tài liệu.</summary>
public sealed class PolicyRulesApiTests(CirculationApiFactory factory) : CirculationTestBase(factory)
{
    private const long Textbook = 1;
    private const long Reference = 3;

    [Fact]
    public async Task Bib_type_rules_limit_loans_loan_days_renewals_and_holds_per_group()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "NT");
        var policy = await Read<LoanPolicyDto>(await staff.PostAsJsonAsync(U("/api/loan-policies/Add"), new LoanPolicyRequest(null, null, 14, MaxLoans: 5,
            BibTypeRules: [new BibTypeRule { BibTypeId = Reference, LoanDays = 3, MaxLoans = 1, MaxRenewals = 0, MaxHolds = 0 }]), Json));
        Assert.Equal((Reference, 3), (Assert.Single(policy.BibTypeRules!).BibTypeId, policy.BibTypeRules![0].LoanDays));
        var dup = await staff.PostAsJsonAsync(U("/api/loan-policies/Add"), new LoanPolicyRequest(7, null,
            BibTypeRules: [new BibTypeRule { BibTypeId = 4 }, new BibTypeRule { BibTypeId = 4 }]), Json);
        Assert.Equal("BIB_TYPE_RULE_DUPLICATE", (await Error(dup)).Code);

        long refA = NextMfn(), refB = NextMfn(), book = NextMfn();
        await Publish(Reader(tenantId, "NT-01"),
            Bib(tenantId, refA, "Từ điển Anh-Việt") with { BibTypeId = Reference }, Bib(tenantId, refB, "Bách khoa thư") with { BibTypeId = Reference },
            Bib(tenantId, book, "Toán 6") with { BibTypeId = Textbook },
            Item(tenantId, "TD01", refA), Item(tenantId, "TD02", refA), Item(tenantId, "BK01", refB), Item(tenantId, "SGK01", book));

        var result = await Read<CheckoutResult>(await Checkout(staff, "NT-01", place.Id, "TD01", "BK01", "SGK01"));
        Assert.Equal(2, result.Succeeded);
        Assert.Contains("thuộc nhóm này", result.Lines.Single(l => l.Barcode == "BK01").Message, StringComparison.Ordinal);
        var reference = result.Lines.Single(l => l.Barcode == "TD01").Loan!;
        var textbook = result.Lines.Single(l => l.Barcode == "SGK01").Loan!;
        Assert.Equal((3.0, 14.0), ((reference.DueAt - reference.LoanedAt).TotalDays, (textbook.DueAt - textbook.LoanedAt).TotalDays));

        // Gia hạn: nhóm tham khảo không cho (0 lần), sách giáo khoa theo chính sách chung.
        Assert.Equal("RENEW_LIMIT", (await Error(await staff.PostAsJsonAsync(U("/api/loans/Renew"), new RenewRequest(reference.PublicId, "Ôn thi"), Json))).Code);
        Assert.Equal(1, (await Read<LoanDto>(await staff.PostAsJsonAsync(U("/api/loans/Renew"), new RenewRequest(textbook.PublicId, "Ôn thi"), Json))).RenewCount);

        // Đặt mượn: nhóm tham khảo không cho đặt; nhóm khác được.
        var hold = await staff.PostAsJsonAsync(U("/api/holds/Place"), new PlaceHoldRequest("NT-01", Mfn: refB, CircPlaceId: place.Id), Json);
        Assert.Equal("HOLD_LIMIT", (await Error(hold)).Code);
    }

    [Fact]
    public async Task Snapshot_without_a_known_type_is_refreshed_from_catalog_before_applying_the_rule()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "CU");
        await Read<LoanPolicyDto>(await staff.PostAsJsonAsync(U("/api/loan-policies/Add"), new LoanPolicyRequest(null, null, 14,
            BibTypeRules: [new BibTypeRule { BibTypeId = Reference, LoanDays = 2 }]), Json));
        var mfn = NextMfn();
        var bib = Bib(tenantId, mfn, "Atlat địa lý") with { BibTypeId = Reference, Version = 4 };
        await Publish(Reader(tenantId, "CU-01"), Item(tenantId, "AT01", mfn));
        using (var scope = Factory.Services.CreateScope())
        {
            // Bản sao từ trước khi có cột loại biểu ghi: cùng phiên bản, chưa có loại.
            using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
            var db = scope.ServiceProvider.GetRequiredService<CirculationDbContext>();
            db.Set<BibSnapshot>().Add(new BibSnapshot { BibPublicId = bib.BibPublicId, Mfn = mfn, Title = bib.Title, Version = 4, TypeKnown = false });
            await db.SaveChangesAsync();
        }
        Factory.Sources.Bibs[(tenantId, mfn)] = bib;

        var loan = (await Read<CheckoutResult>(await Checkout(staff, "CU-01", place.Id, "AT01"))).Lines[0].Loan!;
        Assert.Equal(2.0, (loan.DueAt - loan.LoanedAt).TotalDays);
    }

    [Fact]
    public async Task Owing_a_document_adds_tech_and_shipping_fees_once_per_ticket()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "PH");
        await Read<LoanPolicyDto>(await staff.PostAsJsonAsync(U("/api/loan-policies/Add"), new LoanPolicyRequest(null, null, 14,
            TechFee: 15000, ShippingFee: 5000), Json));
        var mfn = NextMfn();
        await Publish(Reader(tenantId, "PH-09"), Bib(tenantId, mfn, "Dế mèn"), Item(tenantId, "DM01", mfn), Item(tenantId, "DM02", mfn));
        var loans = (await Read<CheckoutResult>(await Checkout(staff, "PH-09", place.Id, "DM01", "DM02"))).Lines.Select(l => l.Loan!.PublicId).ToList();

        // Cán bộ tích hai lượt chưa quá hạn → hai dòng "Mất tài liệu" + một dòng XLKY + một dòng VANCHUYEN.
        var built = await Read<FineTicketDetail>(await staff.PostAsJsonAsync(U("/api/fine-tickets/BuildForReader"),
            new BuildFineTicketRequest("PH-09", place.Id, loans), Json));
        Assert.Equal(["MATTL", "MATTL", "VANCHUYEN", "XLKY"], built.Lines.Select(l => l.ReasonCode).Order());
        Assert.Equal(20000m, built.Lines.Where(l => l.ReasonCode is "XLKY" or "VANCHUYEN").Sum(l => l.Amount));

        // Gom lại không cộng phí lần nữa.
        var again = await Read<FineTicketDetail>(await staff.PostAsJsonAsync(U("/api/fine-tickets/BuildForReader"), new BuildFineTicketRequest("PH-09"), Json));
        Assert.Equal(4, again.Lines.Count);
    }
}
