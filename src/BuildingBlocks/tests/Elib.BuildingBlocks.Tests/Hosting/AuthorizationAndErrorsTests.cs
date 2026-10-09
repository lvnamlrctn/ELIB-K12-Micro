using System.Net;
using System.Text.Json;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.Testing;
using Microsoft.AspNetCore.Builder;

namespace Elib.BuildingBlocks.Tests.Hosting;

public sealed class AuthorizationAndErrorsTests : IAsyncLifetime
{
    private const string Staff1 = "sub=1;sub_type=staff;tenant_id=3;pstamp=s1";
    private const string Reader = "sub=7;sub_type=reader;tenant_id=3";
    private const string SuperAdmin = "sub=99;sub_type=staff";

    private readonly FakePermissionSource _permissions = new();
    private readonly FakeLicenseSource _licenses = new();
    private WebApplication _app = null!;

    public async Task InitializeAsync()
    {
        _app = await TestApp.StartAsync(_permissions, _licenses, app =>
        {
            app.MapGet("/news", [Permission("NEWS", "add")] () => "ok");
            app.MapGet("/news-view", [Permission("NEWS", "view")] () => "ok");
            app.MapGet("/news-any", [PermissionAny("NEWS:edit", "NEWS:add")] () => "ok");
            app.MapGet("/loans", [RequiresModule("CIRCULATION")] (ITenantContext t) => t.TenantId);
            app.MapGet("/rule", () => { throw new BusinessRuleException("LOAN_LIMIT_EXCEEDED", "Vượt hạn mức"); });
            app.MapGet("/missing", () => { throw new NotFoundException("Bib", 5); });
            app.MapGet("/cross", () => { throw new TenantAccessDeniedException("x"); });
            app.MapGet("/boom", () => { throw new InvalidOperationException("secret detail"); });
        });
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    private static async Task<(HttpStatusCode Status, string? Code, string Body)> Get(HttpClient client, string url)
    {
        var response = await client.GetAsync(new Uri(url, UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();
        string? code = null;
        if (response.Content.Headers.ContentType?.MediaType == "application/problem+json")
            code = JsonDocument.Parse(body).RootElement.GetProperty("code").GetString();
        return (response.StatusCode, code, body);
    }

    [Fact]
    public async Task Permission_granted()
    {
        _permissions.Grants[1] = ["NEWS:add"];
        Assert.Equal(HttpStatusCode.OK, (await Get(_app.Client(Staff1), "/news")).Status);
    }

    [Fact]
    public async Task Permission_missing_returns_403_with_code()
    {
        _permissions.Grants[1] = ["NEWS:view"];
        var r = await Get(_app.Client(Staff1), "/news");
        Assert.Equal(HttpStatusCode.Forbidden, r.Status);
        Assert.Equal(ElibErrorCodes.PermissionDenied, r.Code);
    }

    [Fact]
    public async Task Permission_any_matches_one_of()
    {
        _permissions.Grants[1] = ["NEWS:edit"];
        Assert.Equal(HttpStatusCode.OK, (await Get(_app.Client(Staff1), "/news-any")).Status);
    }

    [Fact]
    public async Task Wildcard_grants_everything()
    {
        _permissions.Grants[99] = [PermissionCodes.All];
        Assert.Equal(HttpStatusCode.OK, (await Get(_app.Client(SuperAdmin), "/news")).Status);
    }

    [Fact]
    public async Task Readonly_impersonation_gets_view_only_without_asking_identity()
    {
        const string impersonating = "sub=99;sub_type=staff;tenant_id=3;imp=readonly";
        _permissions.Grants[99] = [PermissionCodes.All]; // quyền hệ thống của sysadmin KHÔNG được dùng trong đơn vị
        var calls = _permissions.Calls;
        Assert.Equal(HttpStatusCode.OK, (await Get(_app.Client(impersonating), "/news-view")).Status);
        var write = await Get(_app.Client(impersonating), "/news");
        Assert.Equal(HttpStatusCode.Forbidden, write.Status);
        Assert.Equal(ElibErrorCodes.ImpersonationReadOnly, write.Code);
        Assert.Equal(HttpStatusCode.Forbidden, (await Get(_app.Client("sub=99;sub_type=staff;tenant_id=3;imp=full"), "/news-view")).Status);
        Assert.Equal(calls, _permissions.Calls);
    }

    [Fact]
    public async Task Reader_cannot_use_staff_permission()
    {
        _permissions.Grants[7] = ["NEWS:add"];
        Assert.Equal(HttpStatusCode.Forbidden, (await Get(_app.Client(Reader), "/news")).Status);
    }

    [Fact]
    public async Task Anonymous_gets_401()
    {
        var r = await Get(_app.Client(), "/news");
        Assert.Equal(HttpStatusCode.Unauthorized, r.Status);
        Assert.Equal("UNAUTHENTICATED", r.Code);
    }

    [Fact]
    public async Task Permission_source_failure_denies()
    {
        _permissions.Throw = true;
        Assert.Equal(HttpStatusCode.Forbidden, (await Get(_app.Client("sub=2;sub_type=staff;tenant_id=3"), "/news")).Status);
    }

    [Fact]
    public async Task Permissions_are_cached_per_stamp()
    {
        _permissions.Grants[5] = ["NEWS:add"];
        await Get(_app.Client("sub=5;sub_type=staff;tenant_id=3;pstamp=a"), "/news");
        await Get(_app.Client("sub=5;sub_type=staff;tenant_id=3;pstamp=a"), "/news");
        Assert.Equal(1, _permissions.Calls);

        await Get(_app.Client("sub=5;sub_type=staff;tenant_id=3;pstamp=b"), "/news");
        Assert.Equal(2, _permissions.Calls);
    }

    [Fact]
    public async Task Module_licensed_allows()
    {
        _licenses.Licensed.Add((3, "CIRCULATION"));
        var r = await Get(_app.Client(Reader), "/loans");
        Assert.Equal(HttpStatusCode.OK, r.Status);
        Assert.Equal("3", r.Body);
    }

    [Fact]
    public async Task Module_not_licensed_returns_403_with_code()
    {
        var r = await Get(_app.Client(Reader), "/loans");
        Assert.Equal(HttpStatusCode.Forbidden, r.Status);
        Assert.Equal(ElibErrorCodes.ModuleNotLicensed, r.Code);
    }

    [Fact]
    public async Task System_context_bypasses_module_license()
    {
        Assert.Equal(HttpStatusCode.OK, (await Get(_app.Client(SuperAdmin), "/loans")).Status);
    }

    [Theory]
    [InlineData("/rule", HttpStatusCode.BadRequest, "LOAN_LIMIT_EXCEEDED")]
    [InlineData("/missing", HttpStatusCode.NotFound, "NOT_FOUND")]
    [InlineData("/cross", HttpStatusCode.Forbidden, "TENANT_ACCESS_DENIED")]
    [InlineData("/boom", HttpStatusCode.InternalServerError, "INTERNAL_ERROR")]
    public async Task Exceptions_map_to_problem_details(string url, HttpStatusCode status, string code)
    {
        var r = await Get(_app.Client(), url);
        Assert.Equal(status, r.Status);
        Assert.Equal(code, r.Code);
        Assert.DoesNotContain("secret detail", r.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Health_endpoints_respond()
    {
        Assert.Equal(HttpStatusCode.OK, (await Get(_app.Client(), "/healthz")).Status);
        Assert.Equal(HttpStatusCode.OK, (await Get(_app.Client(), "/ready")).Status);
    }
}
