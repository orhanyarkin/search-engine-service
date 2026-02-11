namespace SearchEngine.Application.DTOs;

/// <summary>
/// İçerik öğelerini ve meta verileri barındıran sayfalanmış arama yanıtı.
/// </summary>
public sealed record SearchResponse(
    List<ContentDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
