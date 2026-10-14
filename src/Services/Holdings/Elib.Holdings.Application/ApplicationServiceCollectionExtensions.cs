using Elib.BuildingBlocks.Crud;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Holdings.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddHoldingsApplication(this IServiceCollection services)
    {
        services.AddCrudResource<StoreTypeResource>();
        services.AddCrudResource<StoreResource>();
        services.AddCrudResource<ItemResource>();
        services.AddCrudResource<ExportReasonResource>();
        services.AddCrudResource<BookOutUnitResource>();
        services.AddCrudResource<ExhibitionLocationResource>();
        services.AddCrudResource<InventoryResource>();
        services.AddCrudResource<StoreMoveResource>();
        services.AddCrudResource<DeliveryResource>();
        services.AddScoped<BibSnapshots>();
        services.AddScoped<ItemEvents>();
        services.AddScoped<HoldingsExcel>();
        services.AddScoped<LostBooks>();
        services.AddScoped<Liquidations>();
        services.AddScoped<StoreOuts>();
        services.AddScoped<BarcodeReRegistration>();
        services.AddScoped<StoreReports>();
        return services;
    }
}
