using SearchEngine.Domain.Enums;

namespace SearchEngine.Domain.Entities;

/// <summary>
/// Harici bir sağlayıcıdan toplanan bir içerik öğesini temsil eder.
/// </summary>
public class Content
{
    /// <summary>Dahili benzersiz tanımlayıcı.</summary>
    public Guid Id { get; set; }

    /// <summary>Harici sağlayıcıdan gelen orijinal tanımlayıcı.</summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>İçerik başlığı.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>İçerik türü (Video veya Makale).</summary>
    public ContentType ContentType { get; set; }

    /// <summary>Bu içeriği sağlayan kaynağın adı.</summary>
    public string SourceProvider { get; set; } = string.Empty;

    // Videoya özgü metrikler
    /// <summary>Görüntülenme sayısı (yalnızca video).</summary>
    public int? Views { get; set; }

    /// <summary>Beğeni sayısı (yalnızca video).</summary>
    public int? Likes { get; set; }

    /// <summary>Süre dizesi, örn. "15:30" (yalnızca video).</summary>
    public string? Duration { get; set; }

    // Makaleye özgü metrikler
    /// <summary>Tahmini okuma süresi (dakika) (yalnızca makale).</summary>
    public int? ReadingTime { get; set; }

    /// <summary>Tepki sayısı (yalnızca makale).</summary>
    public int? Reactions { get; set; }

    /// <summary>Yorum sayısı (yalnızca makale).</summary>
    public int? Comments { get; set; }

    /// <summary>Orijinal yayınlanma tarihi.</summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>İçerikle ilişkili etiketler veya kategoriler.</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Puanlama algoritmasına göre hesaplanan nihai puan.</summary>
    public double FinalScore { get; set; }

    /// <summary>Son başarılı senkronizasyonun zaman damgası.</summary>
    public DateTime LastSyncedAt { get; set; }
}
