using SearchEngine.Domain.Entities;
using SearchEngine.Domain.Enums;

namespace SearchEngine.Domain.Interfaces;

/// <summary>
/// Tam metin arama işlemleri için soyutlama (Elasticsearch).
/// </summary>
public interface ISearchService
{
    /// <summary>Tam metin arama için bir grup içerik öğesini indeksler.</summary>
    Task IndexManyAsync(List<Content> contents, CancellationToken cancellationToken = default);

    /// <summary>İsteğe bağlı filtrelerle tam metin arama yapar.</summary>
    Task<(List<Content> Items, int TotalCount)> SearchAsync(
        string keyword,
        ContentType? type,
        SortBy sortBy,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Arama servisinin kullanılabilir olup olmadığını ve indeksin var olup olmadığını kontrol eder.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
