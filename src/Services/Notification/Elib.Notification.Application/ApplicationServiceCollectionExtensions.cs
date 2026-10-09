using Elib.BuildingBlocks.Crud;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Notification.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationApplication(this IServiceCollection services)
    {
        services.AddSingleton<SmtpHostPolicy>();
        services.AddScoped<EmailDispatcher>();
        services.AddScoped<EmailSettingsService>();
        services.AddScoped<NotificationLogQueries>();
        services.AddCrudResource<EmailTemplateResource>();
        return services;
    }
}
