using Elib.BuildingBlocks.Crud;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Space.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddSpaceApplication(this IServiceCollection services)
    {
        services.AddScoped<Replicas>();
        services.AddCrudResource<BuildingResource>();
        services.AddCrudResource<FloorResource>();
        services.AddCrudResource<SpaceResource>();
        services.AddCrudResource<FixtureResource>();
        services.AddScoped<ShelfEvents>();
        services.AddCrudResource<ShelfRowResource>();
        services.AddCrudResource<FixtureEquipmentResource>();
        services.AddCrudResource<FloorUtilityResource>();
        services.AddCrudResource<LockerCabinetResource>();
        services.AddCrudResource<LockerCompartmentResource>();
        services.AddCrudResource<LockerLoanResource>();
        services.AddScoped<HoursResource>();
        services.AddCrudResource<SpecialDayResource>();
        services.AddScoped<SettingsResource>();
        services.AddScoped<SpaceReaderNotifier>();
        services.AddCrudResource<BanResource>();
        services.AddCrudResource<BookingResource>();
        services.AddScoped<SpaceExcel>();
        services.AddScoped<ReportResource>();
        services.AddScoped<SpaceDashboardService>();
        services.AddCrudResource<AccessDeviceResource>();
        services.AddCrudResource<AccessStaffCardResource>();
        services.AddCrudResource<AccessScanLogResource>();
        services.AddScoped<AccessControlService>();
        return services;
    }
}
