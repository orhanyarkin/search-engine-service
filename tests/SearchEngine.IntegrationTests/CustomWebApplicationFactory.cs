using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Moq;
using SearchEngine.Application.Commands;
using SearchEngine.Domain.Entities;
using SearchEngine.Domain.Enums;
using SearchEngine.Domain.Interfaces;
using SearchEngine.Infrastructure.Data;
using SearchEngine.Infrastructure.Services;
using StackExchange.Redis;

namespace SearchEngine.IntegrationTests;

/// <summary>
/// Entegrasyon testleri için özelleştirilmiş WebApplicationFactory.
/// Gerçek PostgreSQL/Redis/ES/RabbitMQ yerine bellek içi veritabanı ve stub servisler kullanır.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "TestDb_" + Guid.NewGuid();

    // appsettings.json ile aynı değerler kullanılmalı (Program.cs derleme zamanında okuyor)
    private const string TestJwtSecret = "SearchEngine-SuperSecretKey-2024-MinimumLength32Chars!!!";
    private const string TestJwtIssuer = "SearchEngine.WebAPI";
    private const string TestJwtAudience = "SearchEngine.Dashboard";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // EF Core: PostgreSQL yerine bellek içi veritabanı — tüm DbContext ilişkili kayıtları kaldır
            var dbDescriptors = services.Where(d =>
                d.ServiceType == typeof(AppDbContext) ||
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType.FullName?.Contains("DbContext") == true ||
                d.ImplementationType?.FullName?.Contains("Npgsql") == true).ToList();
            foreach (var d in dbDescriptors)
                services.Remove(d);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Redis: Stub
            RemoveService<IConnectionMultiplexer>(services);
            var redisMock = new Mock<IConnectionMultiplexer>();
            services.AddSingleton(redisMock.Object);

            // Önbellek: Stub (işlem yapmaz)
            RemoveService<ICacheService>(services);
            services.AddSingleton<ICacheService>(new StubCacheService());

            // Elasticsearch: Stub
            RemoveService<ISearchService>(services);
            services.AddSingleton<ISearchService>(new StubSearchService());

            // Senkronizasyon Servisi: Stub (HTTP sağlayıcı çağrısı yapmasın)
            RemoveService<ISyncService>(services);
            RemoveService<ProviderSyncService>(services);
            services.AddScoped<ISyncService, StubSyncService>();
            services.AddScoped<ProviderSyncService>(sp =>
                throw new InvalidOperationException("ProviderSyncService testlerde kullanılmamalı"));

            // MassTransit: Tüm mevcut kayıtları kaldır (tekrarlanan sağlık kontrolü önleme)
            var massTransitDescriptors = services.Where(d =>
                d.ServiceType.FullName?.Contains("MassTransit") == true ||
                d.ImplementationType?.FullName?.Contains("MassTransit") == true).ToList();
            foreach (var d in massTransitDescriptors)
                services.Remove(d);

            // Mevcut sağlık kontrolü kayıtlarını kaldır (masstransit-bus tekrar önleme)
            var healthCheckDescriptors = services.Where(d =>
                d.ServiceType.FullName?.Contains("HealthCheck") == true).ToList();
            foreach (var d in healthCheckDescriptors)
                services.Remove(d);

            // MassTransit bellek içi test donanımı + yeni sağlık kontrolü kayıtları
            services.AddMassTransitTestHarness();
            services.AddHealthChecks();

            // Background sync servisi kaldır (testlerde otomatik çalışmasın)
            var hostedServiceDescriptors = services.Where(
                d => d.ImplementationType?.Name == "ProviderSyncBackgroundService" ||
                     (d.ServiceType == typeof(IHostedService) &&
                      d.ImplementationType?.Name == "ProviderSyncBackgroundService")).ToList();
            foreach (var d in hostedServiceDescriptors)
                services.Remove(d);
        });
    }

    /// <summary>Test DB'sine örnek veri ekler.</summary>
    public async Task SeedDataAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Bellek içi veritabanının oluşturulduğunu doğrula
        await db.Database.EnsureCreatedAsync();

        // Zaten veri varsa tekrar ekleme
        if (db.Contents.Any())
            return;

        db.Contents.AddRange(
            new Content
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ExternalId = "v1",
                Title = "Docker Tutorial",
                ContentType = ContentType.Video,
                SourceProvider = "Provider1_JSON",
                FinalScore = 45.5,
                PublishedAt = DateTime.UtcNow.AddDays(-2),
                Tags = ["docker", "devops"],
                Views = 22000,
                Likes = 1800,
                Duration = "25:15"
            },
            new Content
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                ExternalId = "a1",
                Title = "Kubernetes Best Practices",
                ContentType = ContentType.Article,
                SourceProvider = "Provider2_XML",
                FinalScore = 30.2,
                PublishedAt = DateTime.UtcNow.AddDays(-10),
                Tags = ["kubernetes", "devops"],
                ReadingTime = 12,
                Reactions = 150,
                Comments = 45
            },
            new Content
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                ExternalId = "v2",
                Title = "Go Programming Basics",
                ContentType = ContentType.Video,
                SourceProvider = "Provider1_JSON",
                FinalScore = 38.0,
                PublishedAt = DateTime.UtcNow.AddDays(-5),
                Tags = ["golang", "programming"],
                Views = 15000,
                Likes = 1200,
                Duration = "15:30"
            }
        );

        await db.SaveChangesAsync();
    }

    /// <summary>Test istekleri için JWT token oluşturur.</summary>
    public string GetTestToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestJwtIssuer,
            audience: TestJwtAudience,
            claims: [new Claim(ClaimTypes.Name, "testuser"), new Claim(ClaimTypes.Role, "Admin")],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>JWT Authorization header'lı HttpClient oluşturur.</summary>
    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GetTestToken());
        return client;
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var d in descriptors)
            services.Remove(d);
    }
}

/// <summary>İşlem yapmayan önbellek servisi (entegrasyon testleri için).</summary>
internal class StubCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        => Task.FromResult<T?>(default);

    public Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>Stub Elasticsearch servisi (entegrasyon testleri için). Her zaman kullanılamaz döner.</summary>
internal class StubSearchService : ISearchService
{
    public Task IndexManyAsync(List<Content> contents, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<(List<Content> Items, int TotalCount)> SearchAsync(
        string keyword, ContentType? type, SortBy sortBy, int page, int pageSize,
        CancellationToken cancellationToken = default)
        => Task.FromResult<(List<Content>, int)>(([], 0));

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}

/// <summary>Stub senkronizasyon servisi (entegrasyon testleri için). Gerçek HTTP çağrısı yapmaz.</summary>
internal class StubSyncService : ISyncService
{
    public Task<int> SyncAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}
