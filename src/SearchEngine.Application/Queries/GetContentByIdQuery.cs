using MediatR;
using SearchEngine.Application.DTOs;

namespace SearchEngine.Application.Queries;

/// <summary>
/// ID ile tekil bir içerik öğesini getirmek için sorgu.
/// </summary>
public sealed record GetContentByIdQuery(Guid Id) : IRequest<ContentDto?>;
