using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SearchEngine.Application.DTOs;
using SearchEngine.Application.Queries;
using SearchEngine.Domain.Enums;

namespace SearchEngine.WebAPI.Controllers;

/// <summary>
/// İçerik arama ve getirme endpoint'leri.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Route("api")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly IMediator _mediator;

    public SearchController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Tum saglayicilardan icerik ara ve filtrele.
    /// </summary>
    /// <param name="keyword">Baslik ve etiketlerde aranacak opsiyonel anahtar kelime.</param>
    /// <param name="type">Opsiyonel icerik turu filtresi (video, text veya article).</param>
    /// <param name="sortBy">Siralama: popularity (varsayilan), relevance veya recency.</param>
    /// <param name="page">Sayfa numarasi (varsayilan: 1).</param>
    /// <param name="pageSize">Sayfa basina oge sayisi (varsayilan: 10, maks: 50).</param>
    /// <param name="cancellationToken">Iptal token'i.</param>
    [HttpGet("search")]
    [EnableRateLimiting("search")]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? keyword,
        [FromQuery] string? type,
        [FromQuery] string sortBy = "popularity",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        // İçerik türü eşleme — spec "text" diyor, enum "Article" kullanıyor, ikisi de kabul edilir
        var contentType = type?.ToLowerInvariant() switch
        {
            "video" => (ContentType?)ContentType.Video,
            "text" or "article" => ContentType.Article,
            _ => null
        };
        var sortByEnum = Enum.TryParse<SortBy>(sortBy, true, out var sb) ? sb : SortBy.Popularity;

        var query = new SearchContentsQuery(keyword, contentType, sortByEnum, page, pageSize);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Benzersiz kimlik ile tek bir icerik ogesi getir.
    /// </summary>
    /// <param name="id">Icerik ogesinin GUID degeri.</param>
    /// <param name="cancellationToken">Iptal token'i.</param>
    [HttpGet("contents/{id:guid}")]
    [ProducesResponseType(typeof(ContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetContentByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        return result is not null ? Ok(result) : NotFound();
    }
}
