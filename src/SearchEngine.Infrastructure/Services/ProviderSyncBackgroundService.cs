using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SearchEngine.Infrastructure.Configuration;

namespace SearchEngine.Infrastructure.Services;

/// <summary>
/// Periyodik olarak tüm sağlayıcılardan içerik senkronize eden arka plan servisi.
/// TrySyncAllAsync kullanarak başka bir sync zaten çalışıyorsa çakışmadan atlar.
/// </summary>
public class ProviderSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProviderSyncBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public ProviderSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ProviderSyncBackgroundService> logger,
        IOptions<BackgroundSyncSettings> settings)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(settings.Value.IntervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Arka plan sync servisi başlatıldı. Aralık: {Interval} dakika.", _interval.TotalMinutes);

        // İlk background sync öncesi bekle (başlangıç sync'i startup'ta çalışır)
        await Task.Delay(_interval, stoppingToken);

        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Arka plan sync tetiklendi...");

                using var scope = _scopeFactory.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<ProviderSyncService>();
                var count = await syncService.TrySyncAllAsync(stoppingToken);

                if (count >= 0)
                    _logger.LogInformation("Arka plan sync tamamlandı. {Count} öğe işlendi.", count);
                else
                    _logger.LogInformation("Arka plan sync atlandı — başka bir sync zaten çalışıyor.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Arka plan sync başarısız oldu.");
            }
        }
    }
}
