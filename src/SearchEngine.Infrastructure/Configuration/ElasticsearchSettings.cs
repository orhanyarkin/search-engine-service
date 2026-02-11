namespace SearchEngine.Infrastructure.Configuration;

/// <summary>
/// Elasticsearch baglanti yapilandirmasi.
/// </summary>
public class ElasticsearchSettings
{
    /// <summary>Elasticsearch sunucu URL'si.</summary>
    public string Url { get; set; } = "http://localhost:9200";
}
