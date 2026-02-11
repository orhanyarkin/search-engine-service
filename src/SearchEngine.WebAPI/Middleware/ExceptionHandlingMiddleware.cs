using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace SearchEngine.WebAPI.Middleware;

/// <summary>
/// Global hata yakalama middleware'i. ProblemDetails formatında yanıt döner.
/// CorrelationId'yi yanıta ekler. ValidationException için 400 Bad Request döner.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // İstemci bağlantıyı kesti — yanıt yazmaya gerek yok
        if (exception is OperationCanceledException)
        {
            _logger.LogInformation("İstek iptal edildi (istemci bağlantıyı kesti).");
            context.Response.StatusCode = 499; // Client Closed Request
            return;
        }

        var (statusCode, title) = exception switch
        {
            FluentValidation.ValidationException => (HttpStatusCode.BadRequest, "Doğrulama hatası"),
            ValidationException => (HttpStatusCode.BadRequest, "Doğrulama hatası"),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Kaynak bulunamadı"),
            ArgumentException => (HttpStatusCode.BadRequest, "Geçersiz istek"),
            _ => (HttpStatusCode.InternalServerError, "Beklenmeyen bir hata oluştu")
        };

        _logger.LogError(exception, "İşlenmeyen hata: {Message}", exception.Message);

        // CorrelationId'yi al (CorrelationIdMiddleware tarafından set edilir)
        var correlationId = context.Items.TryGetValue("CorrelationId", out var id)
            ? id?.ToString()
            : null;

        // 500 hatalarında iç detayları dışarıya sızdırma
        var detail = statusCode == HttpStatusCode.InternalServerError
            ? "Beklenmeyen bir sunucu hatası oluştu."
            : exception.Message;

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        // CorrelationId'yi ProblemDetails extensions'a ekle
        if (correlationId is not null)
            problemDetails.Extensions["correlationId"] = correlationId;

        // FluentValidation hataları için detaylı hata listesi ekle
        if (exception is FluentValidation.ValidationException validationEx)
        {
            problemDetails.Extensions["errors"] = validationEx.Errors
                .Select(e => new { field = e.PropertyName, message = e.ErrorMessage })
                .ToList();
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
