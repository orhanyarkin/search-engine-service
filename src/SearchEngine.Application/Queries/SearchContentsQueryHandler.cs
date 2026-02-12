using MediatR;
using Microsoft.Extensions.Logging;
using SearchEngine.Application.DTOs;
using SearchEngine.Application.Mapping;
using SearchEngine.Domain.Interfaces;

namespace SearchEngine.Application.Queries;

/// <summary>
/// Arama sorgularını işler. Keyword varsa önce Elasticsearch'ten arar (full-text search),
/// ES kullanılamıyorsa PostgreSQL'e fallback yapar. Keyword yoksa doğrudan DB'den okur.
/// Write DB → Read ES deseni (CQRS read optimization).
/// </summary>
public sealed class SearchContentsQueryHandler : IRequestHandler<SearchContentsQuery, SearchResponse>
{
    private readonly IContentRepository _repository;
    private readonly ISearchService _searchService;
    private readonly ILogger<SearchContentsQueryHandler> _logger;

    public SearchContentsQueryHandler(
        IContentRepository repository,
        ISearchService searchService,
        ILogger<SearchContentsQueryHandler> logger)
    {
        _repository = repository;
        _searchService = searchService;
        _logger = logger;
    }

    public async Task<SearchResponse> Handle(SearchContentsQuery request, CancellationToken cancellationToken)
    {
        List<Domain.Entities.Content> items;
        int totalCount;

        // Keyword varsa ES-first (hata durumunda PostgreSQL'e fallback), yoksa doğrudan DB
        if (!string.IsNullOrWhiteSpace(request.Keyword) && await _searchService.IsAvailableAsync(cancellationToken))
        {
            try
            {
                _logger.LogInformation("Elasticsearch kullanılarak aranıyor: '{Keyword}'", request.Keyword);

                (items, totalCount) = await _searchService.SearchAsync(
                    request.Keyword,
                    request.Type,
                    request.SortBy,
                    request.Page,
                    request.PageSize,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Elasticsearch araması başarısız oldu, PostgreSQL'e fallback yapılıyor.");

                (items, totalCount) = await _repository.SearchAsync(
                    request.Keyword,
                    request.Type,
                    request.SortBy,
                    request.Page,
                    request.PageSize,
                    cancellationToken);
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.Keyword))
                _logger.LogWarning("Elasticsearch kullanılamıyor, PostgreSQL'e fallback yapılıyor.");

            (items, totalCount) = await _repository.SearchAsync(
                request.Keyword,
                request.Type,
                request.SortBy,
                request.Page,
                request.PageSize,
                cancellationToken);
        }

        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new SearchResponse(
            Items: items.ToDtoList(),
            TotalCount: totalCount,
            Page: request.Page,
            PageSize: request.PageSize,
            TotalPages: totalPages);
    }
}
