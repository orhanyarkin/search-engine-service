namespace SearchEngine.Infrastructure.Configuration;

/// <summary>
/// Arka plan senkronizasyon servisi icin yapilandirma ayarlari.
/// </summary>
public class BackgroundSyncSettings
{
    /// <summary>Dakika cinsinden senkronizasyon araligi.</summary>
    public int IntervalMinutes { get; set; } = 30;
}
