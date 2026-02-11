using SearchEngine.Application.DTOs;
using SearchEngine.Domain.Entities;

namespace SearchEngine.Application.Mapping;

/// <summary>
/// Domain entity'leri ve DTO'lar arası manuel esleme.
/// </summary>
public static class ContentMapper
{
    /// <summary>Bir Content entity'sini ContentDto'ya donusturur.</summary>
    public static ContentDto ToDto(this Content content) => new(
        Id: content.Id,
        Title: content.Title,
        ContentType: content.ContentType.ToString(),
        SourceProvider: content.SourceProvider,
        FinalScore: Math.Round(content.FinalScore, 2),
        PublishedAt: content.PublishedAt,
        Tags: content.Tags,
        Views: content.Views,
        Likes: content.Likes,
        Duration: content.Duration,
        ReadingTime: content.ReadingTime,
        Reactions: content.Reactions,
        Comments: content.Comments);

    /// <summary>Content entity listesini ContentDto listesine donusturur.</summary>
    public static List<ContentDto> ToDtoList(this IEnumerable<Content> contents)
        => contents.Select(c => c.ToDto()).ToList();
}
