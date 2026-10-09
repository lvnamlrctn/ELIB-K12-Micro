using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Elib.BuildingBlocks.Tests.Hosting;

public static class TestApp
{
    public static async Task<WebApplication> StartAsync(FakePermissionSource permissions, FakeLicenseSource licenses, Action<WebApplication> map)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration["Auth:Authority"] = "https://identity.test";
        builder.Configuration["Auth:Audience"] = "elib-test";

        builder.AddElibServiceDefaults("test");
        builder.Services.AddHeaderTestAuth();
        builder.Services.AddSingleton<IPermissionSource>(permissions);
        builder.Services.AddSingleton<IModuleLicenseSource>(licenses);

        var app = builder.Build();
        app.UseElibServiceDefaults();
        map(app);
        await app.StartAsync();
        return app;
    }

    public static HttpClient Client(this WebApplication app, string? claims = null)
    {
        var client = app.GetTestClient();
        return claims is null ? client : client.WithClaims(claims);
    }
}
