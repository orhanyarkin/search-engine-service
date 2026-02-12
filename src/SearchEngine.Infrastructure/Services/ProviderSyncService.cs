using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SearchEngine.Application.Commands;
using SearchEngine.Domain.Entities;
using SearchEngine.Domain.Interfaces;
using SearchEngine.Infrastructure.Configuration;
using SearchEngine.Infrastructure.Messaging;

namespace SearchEngine.Infrastructure.Services;

/// <summary>
/// Tüm sağlayıcılardan veri çekme, puanlama ve kalıcı depolamayı orkestra eder.
/// Sync sonrası RabbitMQ üzerinden ProviderDataSyncedEvent yayınlar (async işleme).
/// SemaphoreSlim ile eşzamanlı sync koruması sağlar (race condition önleme).
/// </summary>
public class ProviderSyncService : ISyncService
{
    /// <summary>
    /// Eşzamanlı sync işlemlerini önleyen kilit mekanizması.
    /// Static: farklı DI scope'larında bile tek kilit paylaşılır.
    /// </summary>
    private static readonly SemaphoreSlim _syncLock = new(1, 1);

    private readonly IContentProviderFactory _providerFactory;
    private readonly IContentScorer _scorer;
    private readonly IContentRepository _repository;
    private readonly ISearchService _searchService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<ProviderSyncService> _logger;
    private readonly ProviderSettings _settings;

    public ProviderSyncService(
        IContentProviderFactory providerFactory,
        IContentScorer scorer,
        IContentRepository repository,
        ISearchService searchService,
        IPublishEndpoint publishEndpoint,
        ILogger<ProviderSyncService> logger,
        IOptions<ProviderSettings> settings)
    {
        _providerFactory = providerFactory;
        _scorer = scorer;
        _repository = repository;
        _searchService = searchService;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
        _settings = settings.Value;
    }

    /// <summary>
    /// Tüm kayıtlı sağlayıcılardan içerik senkronize eder.
    /// Kilit alarak eşzamanlı çalışmayı engeller.
    /// </summary>
    public async Task<int> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            return await ExecuteSyncAsync(cancellationToken);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    /// <summary>
    /// Non-blocking sync denemesi. Başka bir sync zaten çalışıyorsa -1 döner.
    /// Background servis tarafından kullanılır (çakışma durumunda skip).
    /// </summary>
    public async Task<int> TrySyncAllAsync(CancellationToken cancellationToken = default)
    {
        if (!await _syncLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogInformation("Sync zaten çalışıyor, bu istek atlanıyor.");
            return -1;
        }

        try
        {
            return await ExecuteSyncAsync(cancellationToken);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    /// <summary>Asıl sync iş mantığı. Kilit alındıktan sonra çağrılır.</summary>
    private async Task<int> ExecuteSyncAsync(CancellationToken cancellationToken)
    {
        var allContents = new List<Content>();

        foreach (var provider in _providerFactory.GetAllProviders())
        {
            try
            {
                var contents = await provider.FetchContentsAsync(cancellationToken);
                _logger.LogInformation("{Provider} sağlayıcısından {Count} öğe çekildi.",
                    provider.ProviderName, contents.Count);
                allContents.AddRange(contents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Provider} sağlayıcısından veri çekilemedi. Diğer sağlayıcılarla devam ediliyor.",
                    provider.ProviderName);
            }
        }

        if (allContents.Count == 0)
        {
            _logger.LogWarning("Hiçbir sağlayıcıdan içerik çekilemedi.");
            return 0;
        }

        // Demo amaçlı tarih ayarlama (yapılandırılmışsa)
        if (_settings.AdjustDatesToNow)
            AdjustPublishedDates(allContents);

        // Puan hesaplama
        foreach (var content in allContents)
            content.FinalScore = _scorer.CalculateScore(content);

        // Veritabanına kaydet
        await _repository.UpsertManyAsync(allContents, cancellationToken);
        _logger.LogInformation("{Count} öğe PostgreSQL'e kaydedildi.", allContents.Count);

        // Elasticsearch'e doğrudan indeksle (RabbitMQ bus başlamadan önce çalışan startup sync için kritik)
        try
        {
            await _searchService.IndexManyAsync(allContents, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Elasticsearch indeksleme başarısız oldu. Arama PostgreSQL'e fallback yapacak.");
        }

        // Async tüketiciler için event yayınla (cache invalidation consumer'ı tetikler)
        await _publishEndpoint.Publish(new ProviderDataSyncedEvent
        {
            ItemCount = allContents.Count,
            SyncedAt = DateTime.UtcNow
        }, cancellationToken);

        _logger.LogInformation("Sync tamamlandı. {Count} öğe işlendi, puanlandı ve event yayınlandı.", allContents.Count);

        return allContents.Count;
    }

    /// <summary>
    /// Yayın tarihlerini şimdiki zamana göre ayarlar, freshness tier'larına yayar.
    /// Eski mock verilerde puanlama formülünün freshness bileşeninin görünür olmasını sağlar.
    /// </summary>
    private static void AdjustPublishedDates(List<Content> contents)
    {
        var sorted = contents.OrderByDescending(c => c.PublishedAt).ToList();
        var now = DateTime.UtcNow;

        // Tazelik katmanlarına yay: 1 hafta içi, 1 ay içi, 3 ay içi, daha eski
        int[] dayOffsets = [-2, -5, -10, -20, -45, -60, -100, -120];

        for (int i = 0; i < sorted.Count; i++)
        {
            var offset = i < dayOffsets.Length ? dayOffsets[i] : -(100 + i * 15);
            sorted[i].PublishedAt = now.AddDays(offset);
        }
    }
}
