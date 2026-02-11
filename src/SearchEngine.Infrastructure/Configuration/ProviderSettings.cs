namespace SearchEngine.Infrastructure.Configuration;

/// <summary>
/// Icerik saglayicilari icin yapilandirma ayarlari.
/// </summary>
public class ProviderSettings
{
    /// <summary>JSON Saglayici 1 icin URL.</summary>
    public string Provider1Url { get; set; } = string.Empty;

    /// <summary>XML Saglayici 2 icin URL.</summary>
    public string Provider2Url { get; set; } = string.Empty;

    /// <summary>HTTP istek zaman asimi (saniye cinsinden).</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Basarisiz istekler icin yeniden deneme sayisi.</summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>Demo amacli olarak sahte veri tarihlerinin simdi'ye gore ayarlanip ayarlanmayacagi.</summary>
    public bool AdjustDatesToNow { get; set; } = true;
}
