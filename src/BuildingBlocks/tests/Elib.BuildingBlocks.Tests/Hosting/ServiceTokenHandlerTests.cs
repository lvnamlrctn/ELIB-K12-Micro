using System.Net;
using System.Text;
using Elib.BuildingBlocks.Authorization;
using Microsoft.Extensions.Options;

namespace Elib.BuildingBlocks.Tests.Hosting;

/// <summary>Service token: giữ tới gần hết hạn; nơi nhận trả 401 (identity đổi khoá ký) thì xin token mới và gửi lại đúng một lần.</summary>
public sealed class ServiceTokenHandlerTests
{
    private sealed class Stub(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }

    private sealed class Factory(HttpMessageHandler identity) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(identity, disposeHandler: false);
    }

    private int _issued;

    private ServiceTokenProvider Provider() => new(new Factory(new Stub(_ =>
    {
        var token = $"t{Interlocked.Increment(ref _issued)}";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($$"""{"access_token":"{{token}}","expires_in":3600}""", Encoding.UTF8, "application/json"),
        });
    })), Options.Create(new ServiceClientOptions { IdentityUrl = "http://identity", ClientId = "svc", ClientSecret = "s" }), TimeProvider.System);

    [Fact]
    public async Task Retries_once_with_a_fresh_token_when_the_cached_one_is_rejected()
    {
        using var provider = Provider();
        var seen = new List<(string? Token, string Body)>();
        using var handler = new ServiceTokenHandler(provider)
        {
            InnerHandler = new Stub(async r =>
            {
                seen.Add((r.Headers.Authorization?.Parameter, r.Content is null ? "" : await r.Content.ReadAsStringAsync()));
                return new HttpResponseMessage(r.Headers.Authorization?.Parameter == "t1" ? HttpStatusCode.Unauthorized : HttpStatusCode.OK);
            }),
        };
        using var client = new HttpClient(handler);

        using var first = await client.PostAsync(new Uri("http://tenant/internal/x"), new StringContent("{\"a\":1}"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal([("t1", "{\"a\":1}"), ("t2", "{\"a\":1}")], seen);

        // Token mới được giữ lại cho lần sau.
        using var second = await client.GetAsync(new Uri("http://tenant/internal/y"));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("t2", seen[^1].Token);
        Assert.Equal(2, _issued);
    }

    [Fact]
    public async Task Gives_up_after_one_retry_when_the_fresh_token_is_also_rejected()
    {
        using var provider = Provider();
        var calls = 0;
        using var handler = new ServiceTokenHandler(provider)
        {
            InnerHandler = new Stub(_ =>
            {
                calls++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            }),
        };
        using var client = new HttpClient(handler);
        using var response = await client.GetAsync(new Uri("http://tenant/internal/x"));
        Assert.Equal((HttpStatusCode.Unauthorized, 2), (response.StatusCode, calls));
    }
}
