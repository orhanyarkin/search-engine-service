using SearchEngine.Domain.Interfaces;

namespace SearchEngine.Infrastructure.Providers;

/// <summary>
/// Icerik saglayicilarini isme gore cozumleyen fabrika.
/// DI ile enjekte edilen IContentProvider uygulamalarinin koleksiyonunu sarmalar.
/// </summary>
public class ContentProviderFactory : IContentProviderFactory
{
    private readonly Dictionary<string, IContentProvider> _providers;

    public ContentProviderFactory(IEnumerable<IContentProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IContentProvider GetProvider(string providerName)
        => _providers.TryGetValue(providerName, out var provider)
            ? provider
            : throw new KeyNotFoundException($"Content provider '{providerName}' is not registered.");

    /// <inheritdoc />
    public IEnumerable<IContentProvider> GetAllProviders() => _providers.Values;
}
