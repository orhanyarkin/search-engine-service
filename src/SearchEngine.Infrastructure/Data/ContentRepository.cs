using Microsoft.EntityFrameworkCore;
using SearchEngine.Domain.Entities;
using SearchEngine.Domain.Enums;
using SearchEngine.Domain.Interfaces;

namespace SearchEngine.Infrastructure.Data;

/// <summary>
/// Icerik deposunun EF Core uygulamasi.
/// </summary>
public class ContentRepository : IContentRepository
{
    private readonly AppDbContext _context;

    public ContentRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<(List<Content> Items, int TotalCount)> SearchAsync(
        string? keyword,
        ContentType? type,
        SortBy sortBy,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Contents.AsNoTracking().AsQueryable();

        // Anahtar kelime ile filtrele (baslik ve etiketlerde buyuk/kucuk harf duyarsiz arama)
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.Title, pattern) ||
                c.Tags.Any(t => EF.Functions.ILike(t, pattern)));
        }

        // Icerik turune gore filtrele
        if (type.HasValue)
            query = query.Where(c => c.ContentType == type.Value);

        // Siralama uygula
        query = sortBy switch
        {
            SortBy.Popularity => query.OrderByDescending(c => c.FinalScore),
            SortBy.Recency => query.OrderByDescending(c => c.PublishedAt),
            SortBy.Relevance when !string.IsNullOrWhiteSpace(keyword) =>
                query.OrderByDescending(c => c.Title.ToLower().StartsWith(keyword.ToLower()))
                     .ThenByDescending(c => c.FinalScore),
            _ => query.OrderByDescending(c => c.FinalScore)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<Content?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Contents
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpsertManyAsync(List<Content> contents, CancellationToken cancellationToken = default)
    {
        // Batch fetch: tek sorguda tüm mevcut kayıtları çek (N+1 sorgu yerine)
        var externalIds = contents.Select(c => c.ExternalId).Distinct().ToList();

        var existingContents = await _context.Contents
            .Where(c => externalIds.Contains(c.ExternalId))
            .ToListAsync(cancellationToken);

        var existingMap = existingContents
            .ToDictionary(c => (c.ExternalId, c.SourceProvider));

        foreach (var content in contents)
        {
            var key = (content.ExternalId, content.SourceProvider);

            if (existingMap.TryGetValue(key, out var existing))
            {
                // Mevcut kaydı güncelle (tracked entity üzerinden)
                existing.Title = content.Title;
                existing.ContentType = content.ContentType;
                existing.Views = content.Views;
                existing.Likes = content.Likes;
                existing.Duration = content.Duration;
                existing.ReadingTime = content.ReadingTime;
                existing.Reactions = content.Reactions;
                existing.Comments = content.Comments;
                existing.PublishedAt = content.PublishedAt;
                existing.Tags = content.Tags;
                existing.FinalScore = content.FinalScore;
                existing.LastSyncedAt = content.LastSyncedAt;
            }
            else
            {
                // Yeni kayıt ekle
                _context.Contents.Add(content);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
