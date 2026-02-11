namespace SearchEngine.Domain.Interfaces;

/// <summary>
/// İçerik sağlayıcılarını ada göre çözümleyen fabrika.
/// </summary>
public interface IContentProviderFactory
{
    /// <summary>Benzersiz adına göre belirli bir sağlayıcıyı getirir.</summary>
    IContentProvider GetProvider(string providerName);

    /// <summary>Kayıtlı tüm içerik sağlayıcılarını getirir.</summary>
    IEnumerable<IContentProvider> GetAllProviders();
}
