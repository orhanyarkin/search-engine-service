using SearchEngine.Domain.Entities;

namespace SearchEngine.Domain.Interfaces;

/// <summary>
/// Ağırlıklı puanlama algoritması kullanarak bir içerik öğesi için nihai puanı hesaplar.
/// </summary>
public interface IContentScorer
{
    /// <summary>Referans tarih olarak DateTime.UtcNow kullanarak puan hesaplar.</summary>
    double CalculateScore(Content content);

    /// <summary>Belirli bir referans tarih kullanarak puan hesaplar (test için kullanışlı).</summary>
    double CalculateScore(Content content, DateTime referenceDate);
}
