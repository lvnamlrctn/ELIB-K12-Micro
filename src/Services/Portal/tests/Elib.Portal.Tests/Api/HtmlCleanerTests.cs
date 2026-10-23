using Elib.Portal.Application;
using Microsoft.Extensions.Options;

namespace Elib.Portal.Tests.Api;

/// <summary>Làm sạch HTML của CMS: nội dung hợp lệ giữ nguyên, mã độc bị loại — đây là hàng rào XSS chính của portal.</summary>
public sealed class HtmlCleanerTests
{
    private const long Tenant = 42;
    private readonly HtmlCleaner _cleaner = new(Options.Create(new PortalOptions()));

    private string Clean(string html) => _cleaner.Clean(html, Tenant) ?? "";

    [Fact]
    public void Formatting_headings_lists_tables_and_links_are_kept()
    {
        var html = "<h2>Tiêu đề</h2><p>Đoạn <strong>đậm</strong>, <em>nghiêng</em>.</p><ul><li>Một</li><li>Hai</li></ul><table><tr><td colspan=\"2\">Ô</td></tr></table>";
        var clean = Clean(html);
        Assert.Contains("<h2>Tiêu đề</h2>", clean, StringComparison.Ordinal);
        Assert.Contains("<strong>đậm</strong>", clean, StringComparison.Ordinal);
        Assert.Contains("<li>Hai</li>", clean, StringComparison.Ordinal);
        Assert.Contains("colspan=\"2\"", clean, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<script>alert(1)</script><p>a</p>", "script")]
    [InlineData("<p onclick=\"alert(1)\">a</p>", "onclick")]
    [InlineData("<img src=\"x\" onerror=\"alert(1)\">", "onerror")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>", "javascript")]
    [InlineData("<a href=\" JaVaScRiPt:alert(1)\">x</a>", "script:")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe>", "iframe")]
    [InlineData("<form action=\"https://evil.example\"><input name=\"p\"></form>", "<form")]
    [InlineData("<p style=\"background:url(javascript:alert(1))\">a</p>", "style")]
    [InlineData("<svg><script>alert(1)</script></svg>", "svg")]
    [InlineData("<object data=\"x\"></object><embed src=\"x\">", "embed")]
    [InlineData("<a href=\"data:text/html;base64,PHNjcmlwdD4=\">x</a>", "data:")]
    [InlineData("<a href=\"//evil.example/x\">x</a>", "evil.example")]
    [InlineData("<meta http-equiv=\"refresh\" content=\"0;url=https://evil.example\">", "refresh")]
    public void Dangerous_markup_is_removed(string html, string forbidden)
    {
        Assert.DoesNotContain(forbidden, Clean(html), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Only_images_uploaded_to_this_tenant_survive()
    {
        var own = $"/s3/media-public/{Tenant}/cms-image/2026/10/{new string('a', 32)}.png";
        var other = $"/s3/media-public/{Tenant + 1}/cms-image/2026/10/{new string('a', 32)}.png";
        var clean = Clean($"<img src=\"{own}\" alt=\"ok\"><img src=\"{other}\"><img src=\"https://tracker.example/p.gif\"><img src=\"data:image/png;base64,AAAA\">");
        Assert.Contains(own, clean, StringComparison.Ordinal);
        Assert.DoesNotContain(other, clean, StringComparison.Ordinal);
        Assert.DoesNotContain("tracker.example", clean, StringComparison.Ordinal);
        Assert.DoesNotContain("data:image", clean, StringComparison.Ordinal);
    }

    [Fact]
    public void External_links_open_safely_in_a_new_tab_and_internal_ones_do_not()
    {
        var clean = Clean("<a href=\"https://example.org/x\">ngoài</a> <a href=\"/tin-tuc\" target=\"_blank\">trong</a>");
        Assert.Contains("target=\"_blank\"", clean, StringComparison.Ordinal);
        Assert.Contains("rel=\"noopener noreferrer nofollow\"", clean, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/tin-tuc\">trong</a>", clean, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_or_markup_only_content_becomes_null()
    {
        Assert.Null(_cleaner.Clean("   ", Tenant));
        Assert.Null(_cleaner.Clean("<script>alert(1)</script>", Tenant));
    }
}
