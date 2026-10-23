using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Digital.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Digital.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ServiceName = "digital";

    public static IServiceCollection AddDigitalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DigitalDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Thiếu ConnectionStrings:DigitalDb (đặt qua biến môi trường ConnectionStrings__DigitalDb).");

        services.AddElibPostgres<DigitalDbContext>(connectionString);
        services.AddCrudDbContext<DigitalDbContext>(ServiceName);
        services.AddTenantReplica<DigitalDbContext>(ServiceName);
        services.AddTenantReplicaBootstrap<DigitalDbContext>(configuration);
        services.AddScoped<ITenantSeeder, DigitalTenantSeeder>();

        // Bản sao chưa có bạn đọc → hỏi patron qua /internal (service token).
        services.AddElibServiceTokens();
        AddSource(services, configuration, HttpReplicaSources.Patron, "Patron:Url");
        services.AddScoped<IReplicaSources, HttpReplicaSources>();

        // Ô chọn liên kết sách in (quyết định #2) — tra cứu trực tiếp catalog, không giữ bản sao.
        AddSource(services, configuration, HttpCatalogBibLookup.Catalog, "Catalog:Url");
        services.AddScoped<ICatalogBibLookup, HttpCatalogBibLookup>();
        services.Configure<DigitalOptions>(configuration.GetSection(DigitalOptions.SectionName));

        // URL có hạn của file tài liệu số — tự kiểm quyền (mượn/chính sách) trước khi xin media.
        AddSource(services, configuration, HttpMediaFileAccess.Media, "Media:Url");
        services.AddScoped<IMediaFileAccess, HttpMediaFileAccess>();

        // Xin vé upload/hoàn tất hộ bạn đọc nộp tài liệu (quyết định #3 của plan giai đoạn 2) — dùng chung client "elib-media".
        services.AddScoped<IMediaUploadTicket, HttpMediaUploadTicket>();

        // Tích hợp ngoài của màn Tài liệu số: tra ISBN (Google Books, Open Library), gợi ý phân loại (Gemini), vector hóa (Ollama) — AI/Ollama tắt khi chưa cấu hình.
        services.AddMemoryCache();
        services.AddHttpClient(IsbnLookup.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(8));
        services.AddScoped<IIsbnLookup, IsbnLookup>();
        services.Configure<DigitalAiOptions>(configuration.GetSection(DigitalAiOptions.SectionName));
        var ai = configuration.GetSection(DigitalAiOptions.SectionName).Get<DigitalAiOptions>() ?? new DigitalAiOptions();
        services.AddHttpClient(GeminiClassificationAi.HttpClientName, c =>
        {
            c.BaseAddress = new Uri(ai.BaseUrl.TrimEnd('/') + "/");
            c.Timeout = TimeSpan.FromSeconds(Math.Clamp(ai.TimeoutSeconds, 5, 300));
        });
        services.AddScoped<IClassificationAi, GeminiClassificationAi>();
        services.Configure<DigitalOllamaOptions>(configuration.GetSection(DigitalOllamaOptions.SectionName));
        var ollama = configuration.GetSection(DigitalOllamaOptions.SectionName).Get<DigitalOllamaOptions>() ?? new DigitalOllamaOptions();
        services.AddHttpClient(OllamaEmbedder.HttpClientName, c =>
        {
            if (!string.IsNullOrWhiteSpace(ollama.BaseUrl)) c.BaseAddress = new Uri(ollama.BaseUrl.TrimEnd('/') + "/");
            c.Timeout = TimeSpan.FromSeconds(Math.Clamp(ollama.TimeoutSeconds, 5, 600));
        });
        services.AddScoped<ITextEmbedder, OllamaEmbedder>();
        services.AddSingleton<IPdfTextExtractor, PdfPigTextExtractor>();
        // Luồng file tài liệu số qua /internal của media (đọc để lập chỉ mục / xuất gói, ghi khi nhập gói DSpace) — thời gian chờ dài vì file tới 200 MB.
        var mediaUrl = configuration["Media:Url"];
        if (string.IsNullOrWhiteSpace(mediaUrl)) throw new InvalidOperationException("Thiếu Media:Url (địa chỉ nội bộ của service).");
        services.AddHttpClient(HttpMediaFileStore.HttpClientName, c =>
            {
                c.BaseAddress = new Uri(mediaUrl.TrimEnd('/') + "/");
                c.Timeout = TimeSpan.FromMinutes(30);
            })
            .AddHttpMessageHandler<ServiceTokenHandler>();
        services.AddScoped<IMediaFileStore, HttpMediaFileStore>();
        services.AddSingleton<EbookContentSyncJob>();
        services.AddHostedService(sp => sp.GetRequiredService<EbookContentSyncJob>());

        services.AddElibMessaging<DigitalDbContext>(ServiceName, configuration, x =>
        {
            x.AddTenantReplicaConsumers<DigitalDbContext>();
            x.AddConsumer<ReaderChangedConsumer>();
            x.AddPermissionCacheInvalidation();
        });
        services.AddSingleton<EbookLoanExpiryJob>();
        services.AddHostedService(sp => sp.GetRequiredService<EbookLoanExpiryJob>());
        services.AddHealthChecks().AddDbContextCheck<DigitalDbContext>("db", tags: [ElibHealthTags.Ready]);
        return services;
    }

    private static void AddSource(IServiceCollection services, IConfiguration configuration, string name, string key)
    {
        var url = configuration[key];
        if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException($"Thiếu {key} (địa chỉ nội bộ của service).");
        services.AddHttpClient(name, c =>
            {
                c.BaseAddress = new Uri(url.TrimEnd('/') + "/");
                c.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<ServiceTokenHandler>();
    }
}
