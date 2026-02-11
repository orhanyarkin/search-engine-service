using SearchEngine.Domain.Entities;

namespace SearchEngine.Domain.Interfaces;

/// <summary>
/// Harici bir sağlayıcıdan içerik çekmek için strateji arayüzü.
/// </summary>
public interface IContentProvider
{
    /// <summary>Bu sağlayıcıyı tanımlayan benzersiz ad.</summary>
    string ProviderName { get; }

    /// <summary>Sağlayıcıdan mevcut tüm içerikleri çeker.</summary>
    Task<List<Content>> FetchContentsAsync(CancellationToken cancellationToken = default);
}
