using MassTransit;
using Microsoft.Extensions.Logging;
using SearchEngine.Domain.Interfaces;

namespace SearchEngine.Infrastructure.Messaging;

/// <summary>
/// ProviderDataSyncedEvent tüketici. Sağlayıcı verisi senkronize edildiğinde
/// Redis önbelleğini temizler. Hata durumunda mesajı acknowledge eder (best-effort).
/// </summary>
public sealed class CacheInvalidationConsumer : IConsumer<ProviderDataSyncedEvent>
{
    private readonly ICacheService _cache;
    private readonly ILogger<CacheInvalidationConsumer> _logger;

    public CacheInvalidationConsumer(ICacheService cache, ILogger<CacheInvalidationConsumer> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProviderDataSyncedEvent> context)
    {
        try
        {
            _logger.LogInformation(
                "Sync eventi alındı ({Count} öğe). Önbellek temizleniyor...",
                context.Message.ItemCount);

            await _cache.RemoveByPrefixAsync("searchengine:");

            _logger.LogInformation("Önbellek RabbitMQ eventi ile temizlendi.");
        }
        catch (Exception ex)
        {
            // Best-effort: Redis hatası sync akışını bozmamalı (poison message önleme)
            _logger.LogError(ex, "Önbellek temizleme başarısız oldu. Mesaj acknowledge ediliyor.");
        }
    }
}
