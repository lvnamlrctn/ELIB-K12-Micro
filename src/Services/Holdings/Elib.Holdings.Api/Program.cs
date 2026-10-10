using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Persistence;
using Elib.Holdings.Api;
using Elib.Holdings.Application;
using Elib.Holdings.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddElibServiceDefaults(InfrastructureServiceCollectionExtensions.ServiceName);
builder.Services.AddHoldingsApplication();
builder.Services.AddHoldingsInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

// Quyền [Permission] hỏi identity qua API nội bộ, bằng service token của chính holdings (client svc-holdings).
builder.Services.AddElibIdentityClient(builder.Configuration);

var app = builder.Build();
if (app.Configuration.GetValue<bool>(DatabaseMigration.MigrateOnStartupKey))
    await app.Services.MigrateElibDatabaseAsync<HoldingsDbContext>();

app.UseElibServiceDefaults();
app.MapHoldingsEndpoints();
app.Run();

/// <summary>Để WebApplicationFactory trong test tham chiếu được.</summary>
public partial class Program;
