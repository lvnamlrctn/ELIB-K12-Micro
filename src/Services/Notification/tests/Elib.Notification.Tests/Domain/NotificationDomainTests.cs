using System.Net;
using Elib.BuildingBlocks.Domain;
using Elib.Notification.Application;
using Elib.Notification.Domain;

namespace Elib.Notification.Tests.Domain;

public sealed class NotificationDomainTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("100.64.0.1", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("192.168.1.10", true)]
    [InlineData("169.254.169.254", true)]
    [InlineData("0.0.0.0", true)]
    [InlineData("fd00::1", true)]
    [InlineData("fe80::1", true)]
    [InlineData("::ffff:10.0.0.1", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("2001:4860:4860::8888", false)]
    public void Private_addresses_are_detected(string ip, bool expected) =>
        Assert.Equal(expected, SmtpHostPolicy.IsPrivate(IPAddress.Parse(ip)));

    [Fact]
    public void Subject_cannot_inject_headers_and_body_is_html_encoded()
    {
        var data = new Dictionary<string, string> { ["name"] = "A\r\nBcc: x@y.vn", ["html"] = "<img src=x onerror=alert(1)>" };
        Assert.Equal("Chào A  Bcc: x@y.vn", TemplateRenderer.RenderSubject("Chào {{ name }}", data));
        Assert.Equal("<p>&lt;img src=x onerror=alert(1)&gt;</p><p></p>", TemplateRenderer.RenderHtml("<p>{{html}}</p><p>{{ missing }}</p>", data));
    }

    [Fact]
    public void Template_and_settings_validation()
    {
        Assert.Equal("LOGIN_OTP", EmailTemplate.NormalizeCode(" login_otp "));
        Assert.Throws<BusinessRuleException>(() => EmailTemplate.NormalizeCode("bad code"));
        Assert.Throws<BusinessRuleException>(() => EmailTemplate.Create("X", "n", "a\nb", "body", null));
        Assert.Throws<BusinessRuleException>(() => EmailSettings.Create("smtp.x.vn", 0, SmtpSecurity.None, null, "a@b.vn", null, null));
        Assert.Throws<BusinessRuleException>(() => EmailSettings.Create("smtp.x.vn", 25, SmtpSecurity.None, null, "Tên <a@b.vn>", null, null));
        Assert.Null(EmailRules.Normalize("không-phải-email"));
        Assert.Equal("a@b.vn", EmailRules.Normalize(" a@b.vn "));
    }
}
