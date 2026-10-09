using Elib.Identity.Infrastructure;

namespace Elib.Identity.Tests;

public sealed class RedirectUriPatternTests
{
    [Theory]
    [InlineData("https://*/admin/callback")]                 // không có tên miền sau '*'
    [InlineData("https://*.vn/admin/callback")]              // quá rộng (một cấp)
    [InlineData("https://truong-*.thuvientn.vn/callback")]   // '*' không đứng đầu host
    [InlineData("https://*.*.thuvientn.vn/callback")]        // nhiều '*'
    [InlineData("https://thuvientn.vn/*")]                   // '*' trong đường dẫn
    [InlineData("*.thuvientn.vn/callback")]                  // thiếu scheme
    public void Unsafe_patterns_are_rejected_at_startup(string pattern) =>
        Assert.Throws<InvalidOperationException>(() => RedirectUriPattern.Compile(pattern));

    [Theory]
    [InlineData("https://truong-a.thuvientn.vn/admin/callback", true)]
    [InlineData("https://TRUONG-A.thuvientn.vn/admin/callback", true)]
    [InlineData("https://-a.thuvientn.vn/admin/callback", false)]
    [InlineData("https://thuvientn.vn/admin/callback", false)]
    [InlineData("https://a.thuvientn.vn/admin/callbackX", false)]
    [InlineData("https://a.thuvientn.vn:8443/admin/callback", false)]
    [InlineData("https://a.thuvientnXvn/admin/callback", false)]    // '.' trong mẫu là ký tự thường, không phải "bất kỳ"
    public void Matching_is_one_dns_label_and_otherwise_exact(string uri, bool expected) =>
        Assert.Equal(expected, RedirectUriPattern.Matches([RedirectUriPattern.Compile("https://*.thuvientn.vn/admin/callback")], uri));
}
