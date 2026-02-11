using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SearchEngine.Application.Commands;

namespace SearchEngine.WebAPI.Controllers;

/// <summary>
/// Sağlayıcı veri senkronizasyonu endpoint'leri.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/providers")]
[Route("api/providers")]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly IMediator _mediator;

    public SyncController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Tüm sağlayıcılardan manuel içerik senkronizasyonu tetikler.
    /// Veri çeker, puanlama yapar ve veritabanını günceller.
    /// GET ve POST her ikisi de desteklenir (case study gereksinimleri uyarınca).
    /// </summary>
    /// <param name="cancellationToken">İptal belirteci.</param>
    [HttpGet("sync")]
    [HttpPost("sync")]
    [ProducesResponseType(typeof(SyncResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SyncProvidersCommand(), cancellationToken);
        return Ok(result);
    }
}
