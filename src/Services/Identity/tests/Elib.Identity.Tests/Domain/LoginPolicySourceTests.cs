using System.Net;
using Elib.Identity.Application;
using Elib.Identity.Infrastructure;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Elib.Identity.Tests.Domain;

public sealed class LoginPolicySourceTests
{
    private sealed class FakeTenant(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler, IHttpClientFactory
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(respond(request));
        }

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false) { BaseAddress = new Uri("http://tenant/") };
    }

    private static (TenantLoginPolicySource Source, FakeTenant Tenant) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var tenant = new FakeTenant(respond);
        var cache = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider().GetRequiredService<HybridCache>();
        var options = Options.Create(new IdentityServerOptions { SystemLogin = new SystemLoginOptions { CaptchaEnabled = true } });
        return (new TenantLoginPolicySource(tenant, cache, options, NullLogger<TenantLoginPolicySource>.Instance), tenant);
    }

    [Fact]
    public async Task Reads_tenant_parameters_caches_and_invalidates()
    {
        string? path = null;
        var (source, tenant) = Create(r =>
        {
            path = r.RequestUri!.PathAndQuery;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ADMIN_LOGIN_CAPTCHA_ENABLED":"1","ADMIN_LOGIN_OTP_ENABLED":"true","READER_AUTH_CONFIG":null}""",
                    System.Text.Encoding.UTF8, "application/json"),
            };
        });

        Assert.Equal(new LoginPolicy(true, true), await source.GetAsync(5, CancellationToken.None));
        Assert.Equal("/internal/tenants/5/parameters?service=identity", path);
        await source.GetAsync(5, CancellationToken.None);
        Assert.Equal(1, tenant.Calls);

        await source.InvalidateAsync(5, CancellationToken.None);
        await source.GetAsync(5, CancellationToken.None);
        Assert.Equal(2, tenant.Calls);

        Assert.Equal(new LoginPolicy(true, false), await source.GetAsync(null, CancellationToken.None)); // tài khoản hệ thống: cấu hình
    }

    [Fact]
    public async Task Tenant_unavailable_turns_captcha_on_without_caching()
    {
        var fail = true;
        var (source, tenant) = Create(_ => fail
            ? throw new HttpRequestException("tenant down")
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") });

        Assert.Equal(new LoginPolicy(Captcha: true, Otp: false), await source.GetAsync(9, CancellationToken.None));
        fail = false;
        Assert.Equal(LoginPolicy.None, await source.GetAsync(9, CancellationToken.None));
        Assert.Equal(2, tenant.Calls);
    }
}
