namespace SearchEngine.Infrastructure.Configuration;

/// <summary>
/// JWT kimlik doğrulama yapılandırma ayarları.
/// </summary>
public class JwtSettings
{
    /// <summary>JWT imzalama için gizli anahtar (en az 32 karakter).</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Token yayınlayıcısı (issuer).</summary>
    public string Issuer { get; set; } = "SearchEngine.WebAPI";

    /// <summary>Token hedef kitlesi (audience).</summary>
    public string Audience { get; set; } = "SearchEngine.Dashboard";

    /// <summary>Token geçerlilik süresi (dakika cinsinden).</summary>
    public int ExpirationMinutes { get; set; } = 60;
}
