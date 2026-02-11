using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using SearchEngine.Domain.Entities;
using SearchEngine.Domain.Enums;
using SearchEngine.Domain.Interfaces;
using SearchEngine.Infrastructure.Providers.Models;

namespace SearchEngine.Infrastructure.Providers;

/// <summary>
/// XML tabanli Saglayici 2 icin icerik saglayici adaptoru.
/// </summary>
public class XmlContentProvider : IContentProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<XmlContentProvider> _logger;
    private static readonly XmlSerializer Serializer = new(typeof(Provider2Feed));

    public XmlContentProvider(IHttpClientFactory httpClientFactory, ILogger<XmlContentProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ProviderName => "Provider2_XML";

    /// <inheritdoc />
    public async Task<List<Content>> FetchContentsAsync(CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("Provider2_XML");

        _logger.LogInformation("{Provider} sağlayıcısından içerik çekiliyor...", ProviderName);

        var response = await client.GetAsync("", cancellationToken);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var feed = (Provider2Feed?)Serializer.Deserialize(stream)
            ?? throw new InvalidOperationException("Sağlayıcı 2 yanıtı deserialize edilemedi.");

        _logger.LogInformation("{Provider} sağlayıcısından {Count} öğe çekildi.", feed.Items.Count, ProviderName);

        return feed.Items.Select(MapToContent).ToList();
    }

    private Content MapToContent(Provider2Item item)
    {
        var isVideo = item.Type.Equals("video", StringComparison.OrdinalIgnoreCase);

        var content = new Content
        {
            Id = GenerateDeterministicGuid(item.Id),
            ExternalId = item.Id,
            Title = item.Headline,
            ContentType = isVideo ? ContentType.Video : ContentType.Article,
            SourceProvider = ProviderName,
            Tags = item.Categories,
            LastSyncedAt = DateTime.UtcNow
        };

        // Yayin tarihini ayristir (format: "2024-03-15" veya tam ISO)
        if (DateTime.TryParse(item.PublicationDate, out var pubDate))
            content.PublishedAt = DateTime.SpecifyKind(pubDate, DateTimeKind.Utc);

        if (isVideo)
        {
            content.Views = ParseInt(item.Stats.Views);
            content.Likes = ParseInt(item.Stats.Likes);
            content.Duration = item.Stats.Duration;
        }
        else
        {
            content.ReadingTime = ParseInt(item.Stats.ReadingTime);
            content.Reactions = ParseInt(item.Stats.Reactions);
            content.Comments = ParseInt(item.Stats.Comments);
        }

        return content;
    }

    private static int? ParseInt(string? value)
        => int.TryParse(value, out var result) ? result : null;

    private Guid GenerateDeterministicGuid(string externalId)
    {
        var input = $"{ProviderName}:{externalId}";
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
}
