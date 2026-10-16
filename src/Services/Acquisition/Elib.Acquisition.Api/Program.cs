using Elib.Acquisition.Api;
using Elib.Acquisition.Application;
using Elib.Acquisition.Infrastructure;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddElibServiceDefaults(InfrastructureServiceCollectionExtensions.ServiceName);
builder.Services.AddAcquisitionApplication();
builder.Services.AddAcquisitionInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

// Quyền [Permission] hỏi identity qua API nội bộ, bằng service token của chính acquisition (client svc-acquisition).
builder.Services.AddElibIdentityClient(builder.Configuration);

var app = builder.Build();
if (app.Configuration.GetValue<bool>(DatabaseMigration.MigrateOnStartupKey))
    await app.Services.MigrateElibDatabaseAsync<AcquisitionDbContext>();

app.UseElibServiceDefaults();
app.MapAcquisitionEndpoints();
app.Run();

/// <summary>Để WebApplicationFactory trong test tham chiếu được.</summary>
public partial class Program;
