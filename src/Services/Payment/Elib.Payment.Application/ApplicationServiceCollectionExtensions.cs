using Microsoft.Extensions.DependencyInjection;

namespace Elib.Payment.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentApplication(this IServiceCollection services)
    {
        services.AddScoped<PaymentAccountService>();
        services.AddScoped<ChargeIngest>();
        services.AddScoped<ChargeBackfill>();
        services.AddScoped<PaymentService>();
        services.AddScoped<WebhookService>();
        services.AddScoped<PaymentExpiry>();
        services.AddScoped<PaymentReconciliation>();
        return services;
    }
}
