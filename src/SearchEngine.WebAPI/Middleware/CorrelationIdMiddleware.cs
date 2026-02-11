using Serilog.Context;

namespace SearchEngine.WebAPI.Middleware;

/// <summary>
/// İstek takibi için CorrelationId middleware'i.
/// Her istekte X-Correlation-Id header'ını okur veya oluşturur,
/// Serilog LogContext'e ekler ve response header'ına yazar.
/// </summary>
public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // İstekten CorrelationId al, yoksa yeni oluştur
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        // HttpContext.Items'a kaydet (diğer middleware'ler erişebilsin)
        context.Items["CorrelationId"] = correlationId;

        // Response header'ına ekle
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        // Serilog LogContext'e pushla — tüm loglar bu CorrelationId'yi içerir
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
