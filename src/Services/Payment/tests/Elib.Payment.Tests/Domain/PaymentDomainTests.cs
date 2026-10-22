using Elib.BuildingBlocks.Domain;
using Elib.Payment.Domain;

namespace Elib.Payment.Tests.Domain;

public sealed class PaymentDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    private static Charge Fine(decimal amount = 50_000, long version = 1) =>
        Charge.Create(ChargeKinds.FineTicket, "Circulation", Guid.NewGuid(), version, Guid.NewGuid(), " HS01 ", "Phiếu phạt PT001", amount);

    private static PaymentTransaction Txn(Charge charge) =>
        PaymentTransaction.Create(charge, PaymentProviders.VietQr, "qr", PaymentTransaction.NewCode(Now), Now, 15);

    [Fact]
    public void A_charge_ignores_older_events()
    {
        var charge = Fine(50_000, 3);
        Assert.False(charge.Apply(ChargeKinds.FineTicket, 3, charge.ReaderPublicId, "HS01", "x", 10_000, null)); // cùng version = cũ
        Assert.Equal(50_000m, charge.Amount);
        Assert.True(charge.Apply(ChargeKinds.FineTicket, 4, charge.ReaderPublicId, "HS01", "Phiếu phạt PT001", 30_000, null));
        Assert.Equal(30_000m, charge.Amount);
    }

    [Fact]
    public void A_paid_charge_follows_the_source_only_after_the_source_acknowledged_that_payment()
    {
        var charge = Fine(50_000, 1);
        charge.MarkPaid(Now, "PT-A");
        Assert.Equal(ChargeStatuses.Paid, charge.Status);

        // Event cũ còn đang bay (chưa phản ánh khoản vừa thu): không mở lại, dù số tiền > 0.
        Assert.False(charge.Apply(ChargeKinds.FineTicket, 2, charge.ReaderPublicId, "HS01", "x", 50_000, null));
        Assert.Equal(ChargeStatuses.Paid, charge.Status);
        Assert.False(charge.Apply(ChargeKinds.FineTicket, 3, charge.ReaderPublicId, "HS01", "x", 50_000, "PT-OTHER"));
        Assert.Equal(ChargeStatuses.Paid, charge.Status);

        // Nguồn xác nhận đã ghi khoản thu, hết nợ: vẫn "đã thu". Rồi phiếu được mở lại / thêm dòng phạt: khoản mở lại, sinh mã mới được.
        Assert.True(charge.Apply(ChargeKinds.FineTicket, 4, charge.ReaderPublicId, "HS01", "x", 0, "PT-A"));
        Assert.Equal(ChargeStatuses.Paid, charge.Status);
        Assert.True(charge.Apply(ChargeKinds.FineTicket, 5, charge.ReaderPublicId, "HS01", "x", 20_000, "PT-A"));
        Assert.True(charge.IsOpen);
        Assert.Null(charge.PaidTransactionCode);

        // Một khoản đã thu qua cổng không bị miễn/đóng đè bởi event muộn.
        var other = Fine(10_000, 1);
        other.MarkPaid(Now, "PT-B");
        other.MarkWaived(Now);
        other.MarkSettled(Now);
        Assert.Equal(ChargeStatuses.Paid, other.Status);
    }

    [Fact]
    public void A_charge_with_nothing_left_to_pay_is_closed_and_bad_amounts_are_refused()
    {
        var charge = Fine(50_000, 1);
        charge.Apply(ChargeKinds.FineTicket, 2, charge.ReaderPublicId, "HS01", "x", 0, null);
        Assert.Equal(ChargeStatuses.Settled, charge.Status);
        charge.Apply(ChargeKinds.FineTicket, 3, charge.ReaderPublicId, "HS01", "x", 20_000, null);
        Assert.True(charge.IsOpen);
        Assert.Equal("AMOUNT_INVALID", Assert.Throws<BusinessRuleException>(() => charge.Apply(ChargeKinds.FineTicket, 4, charge.ReaderPublicId, "HS01", "x", 1_000.5m, null)).Code);
        Assert.Equal("CHARGE_KIND_INVALID", Assert.Throws<BusinessRuleException>(() => charge.Apply("OTHER", 5, charge.ReaderPublicId, "HS01", "x", 1_000, null)).Code);
    }

    [Fact]
    public void Settling_is_idempotent_and_late_money_is_kept_and_flagged_for_review()
    {
        var onTime = Txn(Fine());
        Assert.Equal(SettleOutcome.Paid, onTime.Settle(Now.AddMinutes(5), "REF1", 50_000));
        Assert.Equal(SettleOutcome.AlreadyPaid, onTime.Settle(Now.AddMinutes(6), "REF1", 50_000));
        Assert.False(onTime.NeedsReview);

        var late = Txn(Fine());
        Assert.True(late.Expire(Now.AddMinutes(20)));
        Assert.Equal(SettleOutcome.PaidLate, late.Settle(Now.AddMinutes(25), "REF2", 50_000));
        Assert.True(late.NeedsReview);
        Assert.Equal(TransactionStatuses.PaidLate, late.Status);

        var cancelled = Txn(Fine());
        Assert.True(cancelled.Cancel());
        Assert.Equal(SettleOutcome.PaidLate, cancelled.Settle(Now.AddMinutes(2), "REF3", 50_000));

        var notYet = Txn(Fine());
        Assert.False(notYet.Expire(Now.AddMinutes(14)));
        Assert.True(notYet.IsPending(Now.AddMinutes(14)));
    }

    [Fact]
    public void Transaction_codes_are_unique_and_short_enough_for_a_transfer_note()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => PaymentTransaction.NewCode(Now)).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.All(codes, c => Assert.Equal(20, c.Length));
    }

    [Fact]
    public void Vietnamese_money_format_has_no_decimals()
    {
        Assert.Equal("50.000đ", PaymentAccount.Money(50_000));
    }
}
