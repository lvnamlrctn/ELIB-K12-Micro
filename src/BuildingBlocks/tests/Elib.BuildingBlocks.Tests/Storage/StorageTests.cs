using Elib.BuildingBlocks.Storage;

namespace Elib.BuildingBlocks.Tests.Storage;

public sealed class StorageTests
{
    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, "image/png")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1 }, "image/gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50, 0x56 }, "image/webp")]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 }, "application/pdf")]
    public void Detects_supported_types_by_magic_bytes(byte[] header, string expected) =>
        Assert.Equal(expected, ContentSniffer.Detect(header));

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("<!DOCTYPE html><html>")]
    [InlineData("RIFF....WAVEfmt ")]
    [InlineData("")]
    public void Unsafe_or_unknown_content_is_not_recognised(string text) =>
        Assert.Null(ContentSniffer.Detect(System.Text.Encoding.ASCII.GetBytes(text)));

    [Fact]
    public void Signed_url_keeps_path_and_query_behind_the_gateway_prefix()
    {
        var url = StoragePaths.FromSigned("/s3/", "http://minio:9000/media-private/12/attachment/2026/10/abc.pdf?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Signature=ff");
        Assert.Equal("/s3/media-private/12/attachment/2026/10/abc.pdf?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Signature=ff", url);
        Assert.Equal("/s3/media-public/12/tenant-logo/a.png", StoragePaths.Public("/s3", "media-public", "12/tenant-logo/a.png"));
    }
}
