using Microsoft.Extensions.Options;
using SearchEngine.Domain.Entities;
using SearchEngine.Domain.Enums;
using SearchEngine.Domain.Interfaces;
using SearchEngine.Infrastructure.Configuration;

namespace SearchEngine.Infrastructure.Caching;

/// <summary>
/// Icerik deposunun etrafina onbellekleme davranisi ekleyen dekorator.
/// Dekorator tasarim desenini gosterir.
/// </summary>
public class CachedContentRepository : IContentRepository
{
    private readonly IContentRepository _inner;
    private readonly ICacheService _cache;
    private readonly CacheSettings _settings;

    public CachedContentRepository(
        IContentRepository inner,
        ICacheService cache,
        IOptions<CacheSettings> settings)
    {
        _inner = inner;
        _cache = cache;
        _settings = settings.Value;
    }

    /// <inheritdoc />
    public async Task<(List<Content> Items, int TotalCount)> SearchAsync(
        string? keyword,
        ContentType? type,
        SortBy sortBy,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"searchengine:search:{keyword?.ToLowerInvariant()}:{type}:{sortBy}:{page}:{pageSize}";

        var cached = await _cache.GetAsync<CachedSearchResult>(cacheKey, cancellationToken);
        if (cached is not null)
            return (cached.Items, cached.TotalCount);

        var result = await _inner.SearchAsync(keyword, type, sortBy, page, pageSize, cancellationToken);

        await _cache.SetAsync(
            cacheKey,
            new CachedSearchResult(result.Items, result.TotalCount),
            TimeSpan.FromMinutes(_settings.SearchTtlMinutes),
            cancellationToken);

        return result;
    }

    /// <inheritdoc />
    public async Task<Content?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"searchengine:content:{id}";

        var cached = await _cache.GetAsync<Content>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var result = await _inner.GetByIdAsync(id, cancellationToken);

        if (result is not null)
        {
            await _cache.SetAsync(
                cacheKey,
                result,
                TimeSpan.FromMinutes(_settings.ContentTtlMinutes),
                cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public Task UpsertManyAsync(List<Content> contents, CancellationToken cancellationToken = default)
        => _inner.UpsertManyAsync(contents, cancellationToken);

    /// <summary>Arama sonuclarini onbellege serileştirmek icin dahili kayit.</summary>
    private sealed record CachedSearchResult(List<Content> Items, int TotalCount);
}
