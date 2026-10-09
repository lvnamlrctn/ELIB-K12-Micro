using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Persistence;
using Elib.Media.Api;
using Elib.Media.Application;
using Elib.Media.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddElibServiceDefaults(InfrastructureServiceCollectionExtensions.ServiceName);
builder.Services.AddMediaApplication();
builder.Services.AddMediaInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

// Media không dùng [Permission] (upload theo mục đích + ngữ cảnh đơn vị/hệ thống) — không cần hỏi quyền từ identity.
builder.Services.AddSingleton<IPermissionSource, IdentityNotConnectedPermissionSource>();
builder.Services.AddSingleton<IModuleLicenseSource, NoModuleLicenseSource>();

var app = builder.Build();
if (app.Configuration.GetValue<bool>(DatabaseMigration.MigrateOnStartupKey))
    await app.Services.MigrateElibDatabaseAsync<MediaDbContext>();

app.UseElibServiceDefaults();
app.MapMediaEndpoints();
app.Run();

/// <summary>Để WebApplicationFactory trong test tham chiếu được.</summary>
public partial class Program;
