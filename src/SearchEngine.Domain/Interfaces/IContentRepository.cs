using SearchEngine.Domain.Entities;
using SearchEngine.Domain.Enums;

namespace SearchEngine.Domain.Interfaces;

/// <summary>
/// İçerik kalıcılık işlemleri için depo soyutlaması.
/// </summary>
public interface IContentRepository
{
    /// <summary>İsteğe bağlı filtreleme, sıralama ve sayfalama ile içerik arar.</summary>
    Task<(List<Content> Items, int TotalCount)> SearchAsync(
        string? keyword,
        ContentType? type,
        SortBy sortBy,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Dahili kimliğine göre tek bir içerik öğesi getirir.</summary>
    Task<Content?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>ExternalId + SourceProvider'a göre yeni içerik ekler veya mevcut içeriği günceller.</summary>
    Task UpsertManyAsync(List<Content> contents, CancellationToken cancellationToken = default);
}
