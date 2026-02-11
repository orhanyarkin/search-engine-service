namespace SearchEngine.Infrastructure.Configuration;

/// <summary>
/// RabbitMQ baglanti yapilandirmasi.
/// </summary>
public class RabbitMqSettings
{
    /// <summary>RabbitMQ sunucu adresi.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>RabbitMQ kullanici adi.</summary>
    public string Username { get; set; } = "guest";

    /// <summary>RabbitMQ sifresi.</summary>
    public string Password { get; set; } = "guest";
}
