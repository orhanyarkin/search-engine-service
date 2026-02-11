namespace SearchEngine.Infrastructure.Messaging;

/// <summary>
/// Saglayici verileri basariyla senkronize edilip kaydedildikten sonra yayinlanan olay.
/// Tuketiciler bu olaya onbellek gecersiz kilma, Elasticsearch yeniden indeksleme vb. icin tepki verebilir.
/// </summary>
public record ProviderDataSyncedEvent
{
    /// <summary>Senkronize edilen icerik ogesi sayisi.</summary>
    public int ItemCount { get; init; }

    /// <summary>Senkronizasyon isleminin zaman damgasi.</summary>
    public DateTime SyncedAt { get; init; } = DateTime.UtcNow;
}
