using System.Text.Json;
using Microsoft.Extensions.Logging;
using SearchEngine.Domain.Entities;
using SearchEngine.Domain.Enums;
using SearchEngine.Domain.Interfaces;
using SearchEngine.Infrastructure.Providers.Models;

namespace SearchEngine.Infrastructure.Providers;

/// <summary>
/// JSON tabanli Saglayici 1 icin icerik saglayici adaptoru.
/// </summary>
public class JsonContentProvider : IContentProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JsonContentProvider> _logger;

    public JsonContentProvider(IHttpClientFactory httpClientFactory, ILogger<JsonContentProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ProviderName => "Provider1_JSON";

    /// <inheritdoc />
    public async Task<List<Content>> FetchContentsAsync(CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("Provider1_JSON");

        _logger.LogInformation("{Provider} sağlayıcısından içerik çekiliyor...", ProviderName);

        var response = await client.GetAsync("", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var providerResponse = JsonSerializer.Deserialize<Provider1Response>(json)
            ?? throw new InvalidOperationException("Sağlayıcı 1 yanıtı deserialize edilemedi.");

        _logger.LogInformation("{Provider} sağlayıcısından {Count} öğe çekildi.", providerResponse.Contents.Count, ProviderName);

        return providerResponse.Contents.Select(MapToContent).ToList();
    }

    private Content MapToContent(Provider1Content item)
    {
        var isVideo = item.Type.Equals("video", StringComparison.OrdinalIgnoreCase);

        var content = new Content
        {
            Id = GenerateDeterministicGuid(item.Id),
            ExternalId = item.Id,
            Title = item.Title,
            ContentType = isVideo ? ContentType.Video : ContentType.Article,
            SourceProvider = ProviderName,
            PublishedAt = item.PublishedAt,
            Tags = item.Tags,
            LastSyncedAt = DateTime.UtcNow
        };

        if (isVideo)
        {
            content.Views = item.Metrics.Views;
            content.Likes = item.Metrics.Likes;
            content.Duration = item.Metrics.Duration;
        }
        else
        {
            content.ReadingTime = item.Metrics.ReadingTime;
            content.Reactions = item.Metrics.Reactions;
            content.Comments = item.Metrics.Comments;
        }

        return content;
    }

    private Guid GenerateDeterministicGuid(string externalId)
    {
        var input = $"{ProviderName}:{externalId}";
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
}
