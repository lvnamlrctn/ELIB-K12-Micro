using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.TenantReplica;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Digital.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddDigitalApplication(this IServiceCollection services)
    {
        services.AddScoped<Replicas>();
        services.AddCrudResource<EbookCollectionResource>();
        services.AddCrudResource<EbookSubjectResource>();
        services.AddCrudResource<EbookTopicResource>();
        services.AddCrudResource<EbookDigTypeResource>();
        services.AddCrudResource<PolicyDigitalResource>();
        services.AddCrudResource<PolicyDigitalByCollectionResource>();
        services.AddCrudResource<EbookItemResource>();
        services.AddCrudResource<EbookFileResource>();
        services.AddCrudResource<MetadataSchemaResource>();
        services.AddCrudResource<MetadataFieldResource>();
        services.AddScoped<EbookItemTools>();
        services.AddScoped<DigitalDashboardService>();
        services.AddScoped<PlatformDigitalStatsService>();
        services.AddSharedDataset<SharedEbookDataset>();
        services.AddScoped<SharedSyncControl>();
        services.AddScoped<EbookContentSync>();
        services.AddScoped<OpacContentSearch>();
        services.AddScoped<OpacItems>();
        services.AddScoped<OpacCatalogs>();
        services.AddScoped<EbookReaderNotifier>();
        services.AddScoped<EbookMetadata>();
        services.AddScoped<EbookStatePublisher>();
        services.AddScoped<ReaderReading>();
        services.AddScoped<ReaderBookmarks>();
        services.AddScoped<ReadingTracking>();
        services.AddScoped<EbookReservationAllocator>();
        services.AddCrudResource<EbookLoanResource>();
        services.AddCrudResource<EbookReservationResource>();
        services.AddScoped<EbookFileAccess>();
        services.AddCrudResource<EbookReviewResource>();
        services.AddScoped<OpacReviews>();
        services.AddScoped<ReaderFavorites>();
        services.AddCrudResource<DocumentSubmissionResource>();
        return services;
    }
}
