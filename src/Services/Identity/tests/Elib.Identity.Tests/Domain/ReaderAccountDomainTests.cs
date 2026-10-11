using Elib.BuildingBlocks.Domain;
using Elib.Identity.Domain;

namespace Elib.Identity.Tests.Domain;

public sealed class ReaderAccountDomainTests
{
    private static bool Plain(string password, string hash) => "hash:" + password == hash;

    [Fact]
    public void Older_profile_event_is_ignored()
    {
        var a = new ReaderAccount();
        Assert.True(a.ApplyProfile(2, " hs-1 ", "Nguyễn An", " an@truong.test ", 2, null, false));
        Assert.False(a.ApplyProfile(1, "CU", "Cũ", null, 1, null, false));
        Assert.Equal(("hs-1", "HS-1", "an@truong.test", 2, 2L), (a.CardNo, a.CardKey, a.Email, a.Status, a.Version));
    }

    [Fact]
    public void Card_number_password_and_hashed_password_replace_each_other()
    {
        var a = new ReaderAccount();
        a.ApplyProfile(1, "HS-1", "An", null, 2, null, false);
        Assert.False(a.HasPassword);
        Assert.False(a.VerifyPassword("HS-1", Plain));

        a.UseCardNoPassword();
        Assert.True(a.HasPassword && a.CardNoPassword && a.Credentials.MustChangePassword);
        Assert.True(a.VerifyPassword(" hs-1 ", Plain));
        Assert.False(a.VerifyPassword("", Plain));

        a.SetPassword("hash:bimat123");
        Assert.False(a.CardNoPassword);
        Assert.True(a.VerifyPassword("bimat123", Plain));
        Assert.False(a.VerifyPassword("HS-1", Plain));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("12345")]
    [InlineData("      ")]
    public void Reader_password_needs_six_characters(string? password) =>
        Assert.Equal("READER_PASSWORD_INVALID", Assert.Throws<BusinessRuleException>(() => ReaderAccount.EnsurePasswordRules(password)).Code);
}
