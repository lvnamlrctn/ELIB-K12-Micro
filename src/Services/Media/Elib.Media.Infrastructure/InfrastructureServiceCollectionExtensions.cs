using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Storage;
using Elib.Media.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Media.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ServiceName = "media";

    public static IServiceCollection AddMediaInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MediaDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Thiếu ConnectionStrings:MediaDb (đặt qua biến môi trường ConnectionStrings__MediaDb).");

        services.AddElibPostgres<MediaDbContext>(connectionString);
        services.AddScoped<IMediaDb>(sp => sp.GetRequiredService<MediaDbContext>());
        services.AddElibObjectStorage(configuration);
        services.Configure<MediaOptions>(configuration.GetSection(MediaOptions.SectionName));

        services.AddHostedService<BucketInitializer>();
        services.AddSingleton<PendingUploadCleanup>();
        services.AddHostedService(sp => sp.GetRequiredService<PendingUploadCleanup>());
        services.AddHealthChecks().AddDbContextCheck<MediaDbContext>("db", tags: [ElibHealthTags.Ready]);
        return services;
    }
}
