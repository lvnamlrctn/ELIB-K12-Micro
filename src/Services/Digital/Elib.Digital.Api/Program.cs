using Elib.Digital.Api;
using Elib.Digital.Application;
using Elib.Digital.Infrastructure;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddElibServiceDefaults(InfrastructureServiceCollectionExtensions.ServiceName);
builder.Services.AddDigitalApplication();
builder.Services.AddDigitalInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

// Quyền [Permission] hỏi identity qua API nội bộ, bằng service token của chính digital (client svc-digital).
builder.Services.AddElibIdentityClient(builder.Configuration);

var app = builder.Build();
if (app.Configuration.GetValue<bool>(DatabaseMigration.MigrateOnStartupKey))
    await app.Services.MigrateElibDatabaseAsync<DigitalDbContext>();

app.UseElibServiceDefaults();
app.MapDigitalEndpoints();
app.MapDigitalOpacEndpoints();
app.Run();

/// <summary>Để WebApplicationFactory trong test tham chiếu được.</summary>
public partial class Program;
