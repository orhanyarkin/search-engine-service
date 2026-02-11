using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchEngine.Domain.Interfaces;
using SearchEngine.Infrastructure.Data;

namespace SearchEngine.Infrastructure.Messaging;

/// <summary>
/// ProviderDataSyncedEvent tüketici. Sağlayıcı verisi senkronize edildiğinde
/// tüm içerikleri Elasticsearch'te yeniden indeksler.
/// Hata durumunda mesajı acknowledge eder (best-effort pattern).
/// </summary>
public sealed class ElasticsearchReindexConsumer : IConsumer<ProviderDataSyncedEvent>
{
    private readonly ISearchService _searchService;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ElasticsearchReindexConsumer> _logger;

    public ElasticsearchReindexConsumer(
        ISearchService searchService,
        AppDbContext dbContext,
        ILogger<ElasticsearchReindexConsumer> logger)
    {
        _searchService = searchService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProviderDataSyncedEvent> context)
    {
        try
        {
            _logger.LogInformation(
                "Sync eventi alındı ({Count} öğe). Elasticsearch yeniden indeksleniyor...",
                context.Message.ItemCount);

            var allContents = await _dbContext.Contents
                .AsNoTracking()
                .ToListAsync(context.CancellationToken);

            await _searchService.IndexManyAsync(allContents, context.CancellationToken);

            _logger.LogInformation("Elasticsearch yeniden indeksleme RabbitMQ eventi ile tamamlandı.");
        }
        catch (Exception ex)
        {
            // Best-effort: ES hatası sync akışını bozmamalı (poison message önleme)
            _logger.LogError(ex, "Elasticsearch yeniden indeksleme başarısız oldu. Mesaj acknowledge ediliyor.");
        }
    }
}
