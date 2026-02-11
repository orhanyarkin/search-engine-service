using System.Text.Json.Serialization;

namespace SearchEngine.Infrastructure.Providers.Models;

/// <summary>Sağlayıcı 1 JSON yanıt kök modeli.</summary>
public sealed class Provider1Response
{
    [JsonPropertyName("contents")]
    public List<Provider1Content> Contents { get; set; } = [];

    [JsonPropertyName("pagination")]
    public Provider1Pagination Pagination { get; set; } = null!;
}

/// <summary>Sağlayıcı 1'den gelen tekil içerik öğesi.</summary>
public sealed class Provider1Content
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("metrics")]
    public Provider1Metrics Metrics { get; set; } = null!;

    [JsonPropertyName("published_at")]
    public DateTime PublishedAt { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];
}

/// <summary>Provider 1 içerik metrikleri. Video ve makale türlerini destekler.</summary>
public sealed class Provider1Metrics
{
    // Video metrikleri
    [JsonPropertyName("views")]
    public int? Views { get; set; }

    [JsonPropertyName("likes")]
    public int? Likes { get; set; }

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    // Makale metrikleri
    [JsonPropertyName("reading_time")]
    public int? ReadingTime { get; set; }

    [JsonPropertyName("reactions")]
    public int? Reactions { get; set; }

    [JsonPropertyName("comments")]
    public int? Comments { get; set; }
}

/// <summary>Sağlayıcı 1 sayfalama meta verileri.</summary>
public sealed class Provider1Pagination
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("per_page")]
    public int PerPage { get; set; }
}
