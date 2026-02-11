using MediatR;

namespace SearchEngine.Application.Commands;

/// <summary>
/// Sağlayıcı senkronizasyon komutunu sync servisine devrederek işler.
/// Asıl senkronizasyon mantığı Infrastructure katmanındaki ProviderSyncService'dedir;
/// bu handler MediatR köprüsü görevi görür.
/// </summary>
public sealed class SyncProvidersCommandHandler : IRequestHandler<SyncProvidersCommand, SyncResult>
{
    private readonly ISyncService _syncService;

    public SyncProvidersCommandHandler(ISyncService syncService)
    {
        _syncService = syncService;
    }

    public async Task<SyncResult> Handle(SyncProvidersCommand request, CancellationToken cancellationToken)
    {
        var count = await _syncService.SyncAllAsync(cancellationToken);
        return new SyncResult(count, $"Successfully synced {count} items from all providers.");
    }
}

/// <summary>
/// Application katmanını Infrastructure'dan bağımsız tutmak için sync servis soyutlaması.
/// </summary>
public interface ISyncService
{
    /// <summary>Tüm sağlayıcılardan içerikleri senkronize eder ve işlenen öğe sayısını döndürür.</summary>
    Task<int> SyncAllAsync(CancellationToken cancellationToken = default);
}
