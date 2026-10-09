using System.Security.Claims;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Elib.BuildingBlocks.Tests.Tenancy;

public sealed class TenantResolutionMiddlewareTests
{
    private const string Key = "test-gateway-signing-key-0123456789";

    private readonly TenantContext _tenant = new();
    private readonly CurrentActor _actor = new();
    private bool _nextCalled;

    private Task Run(HttpContext http)
    {
        var middleware = new TenantResolutionMiddleware(
            _ => { _nextCalled = true; return Task.CompletedTask; },
            Options.Create(new TenancyOptions { GatewaySigningKey = Key }));
        return middleware.InvokeAsync(http, _tenant, _actor);
    }

    private static DefaultHttpContext Authenticated(params (string Type, string Value)[] claims) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), authenticationType: "test")),
    };

    [Fact]
    public async Task Token_tenant_and_actor_are_resolved()
    {
        await Run(Authenticated(("sub", "15"), ("sub_type", "staff"), ("tenant_id", "3"), ("tenant_scope", "4, 5")));

        Assert.True(_nextCalled);
        Assert.Equal(3, _tenant.TenantId);
        Assert.Equal([4L, 5L], _tenant.AllowedReadScope);
        Assert.False(_tenant.IsSystem);
        Assert.Equal(15, _actor.Id);
        Assert.Equal("staff", _actor.Kind);
    }

    [Fact]
    public async Task Token_wins_over_header()
    {
        var http = Authenticated(("sub", "1"), ("sub_type", "reader"), ("tenant_id", "3"));
        http.Request.Headers["X-Tenant-Id"] = "9";
        http.Request.Headers["X-Gw-Signature"] = GatewaySignature.Compute(Key, 9);

        await Run(http);

        Assert.Equal(3, _tenant.TenantId);
    }

    [Fact]
    public async Task Staff_without_tenant_is_system()
    {
        await Run(Authenticated(("sub", "1"), ("sub_type", "staff")));

        Assert.True(_tenant.IsSystem);
        Assert.Null(_tenant.TenantId);
    }

    [Fact]
    public async Task Reader_without_tenant_is_rejected()
    {
        var http = Authenticated(("sub", "1"), ("sub_type", "reader"));
        await Run(http);

        Assert.False(_nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, http.Response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_with_valid_gateway_signature_gets_tenant()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "12";
        http.Request.Headers["X-Gw-Signature"] = GatewaySignature.Compute(Key, 12);

        await Run(http);

        Assert.True(_nextCalled);
        Assert.Equal(12, _tenant.TenantId);
        Assert.Null(_actor.Id);
    }

    [Theory]
    [InlineData("12", "bad-signature")]
    [InlineData("abc", "")]
    [InlineData("-1", "")]
    public async Task Anonymous_with_forged_header_is_rejected(string tenantHeader, string signature)
    {
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = tenantHeader;
        http.Request.Headers["X-Gw-Signature"] = signature;

        await Run(http);

        Assert.False(_nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, http.Response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_without_header_stays_unresolved()
    {
        await Run(new DefaultHttpContext());

        Assert.True(_nextCalled);
        Assert.Null(_tenant.TenantId);
        Assert.False(_tenant.IsSystem);
    }
}
