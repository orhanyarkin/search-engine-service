namespace SearchEngine.Domain.Enums;

/// <summary>
/// İçerik arama sonuçları için sıralama seçenekleri.
/// </summary>
public enum SortBy
{
    /// <summary>FinalScore'a göre azalan sıralama (en yüksek puan önce).</summary>
    Popularity,

    /// <summary>Anahtar kelime eşleşme kalitesi ve puan kombinasyonuna göre sıralama.</summary>
    Relevance,

    /// <summary>PublishedAt'e göre azalan sıralama (en yeni önce).</summary>
    Recency
}
