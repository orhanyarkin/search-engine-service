using MediatR;
using SearchEngine.Application.DTOs;
using SearchEngine.Application.Mapping;
using SearchEngine.Domain.Interfaces;

namespace SearchEngine.Application.Queries;

/// <summary>
/// ID ile tekil içerik öğesini getiren handler.
/// </summary>
public sealed class GetContentByIdQueryHandler : IRequestHandler<GetContentByIdQuery, ContentDto?>
{
    private readonly IContentRepository _repository;

    public GetContentByIdQueryHandler(IContentRepository repository)
    {
        _repository = repository;
    }

    public async Task<ContentDto?> Handle(GetContentByIdQuery request, CancellationToken cancellationToken)
    {
        var content = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return content?.ToDto();
    }
}
