using MediatR;

namespace SearchEngine.Application.Commands;

/// <summary>
/// Sağlayıcı veri senkronizasyonunu tetikleyen komut.
/// </summary>
public sealed record SyncProvidersCommand : IRequest<SyncResult>;

/// <summary>
/// Sağlayıcı senkronizasyon işleminin sonucu.
/// </summary>
public sealed record SyncResult(int ItemsSynced, string Message);
