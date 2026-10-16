using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Patron.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Patron.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ServiceName = "patron";

    public static IServiceCollection AddPatronInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PatronDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Thiếu ConnectionStrings:PatronDb (đặt qua biến môi trường ConnectionStrings__PatronDb).");

        services.AddElibPostgres<PatronDbContext>(connectionString);
        services.AddCrudDbContext<PatronDbContext>(ServiceName);
        services.AddTenantReplica<PatronDbContext>(ServiceName);
        services.AddTenantReplicaBootstrap<PatronDbContext>(configuration);
        services.AddScoped<ITenantSeeder, PatronTenantSeeder>();

        // Danh mục đơn vị/dân tộc/trình độ/học hàm ở service tenant — nhập/xuất Excel bạn đọc (service token).
        var tenantUrl = configuration["Tenant:Url"];
        if (string.IsNullOrWhiteSpace(tenantUrl)) throw new InvalidOperationException("Thiếu Tenant:Url (địa chỉ nội bộ của service tenant).");
        services.AddElibServiceTokens();
        services.AddHttpClient(HttpTenantReferences.HttpClientName, c =>
            {
                c.BaseAddress = new Uri(tenantUrl.TrimEnd('/') + "/");
                c.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<ServiceTokenHandler>();
        services.AddScoped<ITenantReferences, HttpTenantReferences>();

        // Lưu thông: loại bạn đọc còn được điểm lưu thông / chính sách dùng thì không cho xoá (service token).
        var circulationUrl = configuration["Circulation:Url"];
        if (string.IsNullOrWhiteSpace(circulationUrl)) throw new InvalidOperationException("Thiếu Circulation:Url (địa chỉ nội bộ của service circulation).");
        services.AddHttpClient(HttpReaderTypeUsage.HttpClientName, c =>
            {
                c.BaseAddress = new Uri(circulationUrl.TrimEnd('/') + "/");
                c.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<ServiceTokenHandler>();
        services.AddScoped<IReaderTypeUsage, HttpReaderTypeUsage>();
        // Nhận diện khuôn mặt (Gemini): tắt khi không có FaceRecognition:ApiKey. Ảnh bạn đọc đọc từ media qua service token.
        services.Configure<Elib.Patron.Application.FaceRecognitionOptions>(configuration.GetSection(Elib.Patron.Application.FaceRecognitionOptions.Section));
        var mediaUrl = configuration["Media:Url"] ?? "http://media:8080";
        services.AddHttpClient(HttpReaderPhotoSource.HttpClientName, c =>
            {
                c.BaseAddress = new Uri(mediaUrl.TrimEnd('/') + "/");
                c.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddHttpMessageHandler<ServiceTokenHandler>();
        services.AddScoped<Elib.Patron.Application.IReaderPhotoSource, HttpReaderPhotoSource>();
        services.AddHttpClient(GeminiFaceMatcher.HttpClientName, c =>
        {
            c.BaseAddress = new Uri(configuration["FaceRecognition:BaseUrl"] ?? "https://generativelanguage.googleapis.com");
            c.Timeout = TimeSpan.FromSeconds(45);
        });
        services.AddScoped<Elib.Patron.Application.IFaceMatcher, GeminiFaceMatcher>();
        services.AddHostedService<ReaderImportWorker>();
        services.AddElibMessaging<PatronDbContext>(ServiceName, configuration, x =>
        {
            x.AddTenantReplicaConsumers<PatronDbContext>();
            x.AddPermissionCacheInvalidation();
        });
        services.AddHealthChecks().AddDbContextCheck<PatronDbContext>("db", tags: [ElibHealthTags.Ready]);
        return services;
    }
}
