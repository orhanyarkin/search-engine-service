namespace SearchEngine.Domain.Interfaces;

/// <summary>
/// Dağıtık önbellekleme işlemleri için soyutlama.
/// </summary>
public interface ICacheService
{
    /// <summary>Anahtara göre önbelleklenmiş değeri getirir.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>Belirtilen süre ile bir değeri önbelleğe kaydeder.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken cancellationToken = default);

    /// <summary>Verilen anahtar önekiyle eşleşen tüm önbellek girdilerini kaldırır.</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}
