namespace SearchEngine.Infrastructure.Configuration;

/// <summary>
/// Onbellekleme icin yapilandirma ayarlari.
/// </summary>
public class CacheSettings
{
    /// <summary>Arama sonucu onbellegi icin dakika cinsinden TTL.</summary>
    public int SearchTtlMinutes { get; set; } = 5;

    /// <summary>Tekil icerik ogesi onbellegi icin dakika cinsinden TTL.</summary>
    public int ContentTtlMinutes { get; set; } = 10;
}
