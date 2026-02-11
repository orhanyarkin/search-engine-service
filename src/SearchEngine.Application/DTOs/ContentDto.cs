using SearchEngine.Domain.Enums;

namespace SearchEngine.Application.DTOs;

/// <summary>
/// API yanıtlarındaki içerik öğeleri için veri transfer nesnesi (DTO).
/// </summary>
public sealed record ContentDto(
    Guid Id,
    string Title,
    string ContentType,
    string SourceProvider,
    double FinalScore,
    DateTime PublishedAt,
    List<string> Tags,
    int? Views,
    int? Likes,
    string? Duration,
    int? ReadingTime,
    int? Reactions,
    int? Comments);
