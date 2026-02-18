using System.Threading.RateLimiting;
using Elastic.Clients.Elasticsearch;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using SearchEngine.Application.Commands;
using SearchEngine.Domain.Interfaces;
using SearchEngine.Infrastructure.Caching;
using SearchEngine.Infrastructure.Configuration;
using SearchEngine.Infrastructure.Data;
using SearchEngine.Infrastructure.Messaging;
using SearchEngine.Infrastructure.Providers;
using SearchEngine.Infrastructure.Search;
using SearchEngine.Infrastructure.Services;
using StackExchange.Redis;

namespace SearchEngine.Infrastructure;

/// <summary>
/// Infrastructure katmani icin bagimlilik enjeksiyonu kaydi.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Tum Infrastructure katmani servislerini kaydeder.</summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Yapilandirma
        services.Configure<ProviderSettings>(configuration.GetSection("ProviderSettings"));
        services.Configure<CacheSettings>(configuration.GetSection("CacheSettings"));
        services.Configure<ElasticsearchSettings>(configuration.GetSection("ElasticsearchSettings"));
        services.Configure<RabbitMqSettings>(configuration.GetSection("RabbitMqSettings"));

        var providerSettings = configuration.GetSection("ProviderSettings").Get<ProviderSettings>()
            ?? new ProviderSettings();

        // Veritabani
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        // Redis
        var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var config = ConfigurationOptions.Parse(redisConnection);
            config.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(config);
        });
        services.AddScoped<ICacheService, RedisCacheService>();

        // Elasticsearch
        var esSettings = configuration.GetSection("ElasticsearchSettings").Get<ElasticsearchSettings>()
            ?? new ElasticsearchSettings();
        services.AddSingleton<ElasticsearchClient>(sp =>
        {
            var settings = new ElasticsearchClientSettings(new Uri(esSettings.Url))
                .DefaultIndex("searchengine-contents")
                .ThrowExceptions(false)
                .RequestTimeout(TimeSpan.FromSeconds(5));
            return new ElasticsearchClient(settings);
        });
        services.AddScoped<ISearchService, ElasticsearchService>();

        // RabbitMQ + MassTransit
        var rabbitSettings = configuration.GetSection("RabbitMqSettings").Get<RabbitMqSettings>()
            ?? new RabbitMqSettings();
        services.AddMassTransit(x =>
        {
            x.AddConsumer<CacheInvalidationConsumer>();
            x.AddConsumer<ElasticsearchReindexConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitSettings.Host, "/", h =>
                {
                    h.Username(rabbitSettings.Username);
                    h.Password(rabbitSettings.Password);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        // Dekorator deseni ile Repository (onbellek sarmalama)
        services.AddScoped<ContentRepository>();
        services.AddScoped<IContentRepository>(sp =>
            new CachedContentRepository(
                sp.GetRequiredService<ContentRepository>(),
                sp.GetRequiredService<ICacheService>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CacheSettings>>()));

        // Icerik Saglayicilari (Strateji deseni)
        services.AddScoped<IContentProvider, JsonContentProvider>();
        services.AddScoped<IContentProvider, XmlContentProvider>();

        // Saglayici Fabrikasi (Fabrika deseni)
        services.AddScoped<IContentProviderFactory, ContentProviderFactory>();

        // Senkronizasyon Servisi
        services.AddScoped<ProviderSyncService>();
        services.AddScoped<ISyncService>(sp => sp.GetRequiredService<ProviderSyncService>());

        // Arka Plan Senkronizasyon Servisi
        services.Configure<BackgroundSyncSettings>(configuration.GetSection("BackgroundSyncSettings"));
        services.AddHostedService<ProviderSyncBackgroundService>();

        // Provider istekleri icin paylasimli istek limiti (saniyede max 10 istek, eszamanli max 5)
        var providerRateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 10,                                        // Kova kapasitesi
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),          // Token yenileme periyodu
            TokensPerPeriod = 5,                                    // Periyot basina eklenen token
            AutoReplenishment = true,
            QueueLimit = 5,                                         // Kuyrukta bekleyecek max istek
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });

        // Standart Dayaniklilik ile HttpClient'lar (Istek Limiti + Yeniden Deneme + Devre Kesici + Zaman Asimi)
        services.AddHttpClient("Provider1_JSON", client =>
        {
            client.BaseAddress = new Uri(providerSettings.Provider1Url);
            client.Timeout = TimeSpan.FromSeconds(providerSettings.TimeoutSeconds);
        })
        .AddStandardResilienceHandler(options =>
        {
            options.RateLimiter.RateLimiter = _ =>
                providerRateLimiter.AcquireAsync(1);
            options.Retry.MaxRetryAttempts = providerSettings.RetryCount;
            options.Retry.Delay = TimeSpan.FromSeconds(1);
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.MinimumThroughput = 5;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient("Provider2_XML", client =>
        {
            client.BaseAddress = new Uri(providerSettings.Provider2Url);
            client.Timeout = TimeSpan.FromSeconds(providerSettings.TimeoutSeconds);
        })
        .AddStandardResilienceHandler(options =>
        {
            options.RateLimiter.RateLimiter = _ =>
                providerRateLimiter.AcquireAsync(1);
            options.Retry.MaxRetryAttempts = providerSettings.RetryCount;
            options.Retry.Delay = TimeSpan.FromSeconds(1);
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.MinimumThroughput = 5;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
