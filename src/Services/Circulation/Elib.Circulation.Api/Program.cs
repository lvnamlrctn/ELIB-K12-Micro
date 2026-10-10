using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Persistence;
using Elib.Circulation.Api;
using Elib.Circulation.Application;
using Elib.Circulation.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddElibServiceDefaults(InfrastructureServiceCollectionExtensions.ServiceName);
builder.Services.AddCirculationApplication();
builder.Services.AddCirculationInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

// Quyền [Permission] hỏi identity qua API nội bộ, bằng service token của chính circulation (client svc-circulation).
builder.Services.AddElibIdentityClient(builder.Configuration);

var app = builder.Build();
if (app.Configuration.GetValue<bool>(DatabaseMigration.MigrateOnStartupKey))
    await app.Services.MigrateElibDatabaseAsync<CirculationDbContext>();

app.UseElibServiceDefaults();
app.MapCirculationEndpoints();
app.Run();

/// <summary>Để WebApplicationFactory trong test tham chiếu được.</summary>
public partial class Program;
