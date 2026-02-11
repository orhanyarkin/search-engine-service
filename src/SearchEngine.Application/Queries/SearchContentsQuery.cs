using MediatR;
using SearchEngine.Application.DTOs;
using SearchEngine.Domain.Enums;

namespace SearchEngine.Application.Queries;

/// <summary>
/// İçerik öğelerini aramak ve filtrelemek için sorgu.
/// </summary>
public sealed record SearchContentsQuery(
    string? Keyword,
    ContentType? Type,
    SortBy SortBy = SortBy.Popularity,
    int Page = 1,
    int PageSize = 10) : IRequest<SearchResponse>;
