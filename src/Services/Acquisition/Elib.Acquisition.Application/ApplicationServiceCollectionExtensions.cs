using Elib.BuildingBlocks.Crud;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Acquisition.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddAcquisitionApplication(this IServiceCollection services)
    {
        services.AddCrudResource<SourceResource>();
        services.AddCrudResource<SupplierResource>();
        services.AddCrudResource<BudgetResource>();
        services.AddCrudResource<FundResource>();
        services.AddCrudResource<PurchaseOrderResource>();
        services.AddCrudResource<ReceiptResource>();
        services.AddScoped<BibSnapshots>();
        services.AddScoped<InternalSuppliers>();
        services.AddScoped<AcquisitionOptionsQuery>();
        services.AddScoped<AcquisitionExcel>();
        services.AddScoped<AcquisitionReports>();
        services.AddScoped<AcquisitionDashboardService>();
        return services;
    }
}
