using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SearchEngine.Application.Behaviors;
using SearchEngine.Application.Services;
using SearchEngine.Domain.Interfaces;

namespace SearchEngine.Application;

/// <summary>
/// Application katmanı bağımlılık enjeksiyonu kayıtları.
/// MediatR, FluentValidation pipeline ve scoring servisleri.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Application katmanı servislerini kaydeder.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // MediatR + Pipeline Behaviors
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // FluentValidation — assembly'deki tüm validator'ları otomatik kaydet
        services.AddValidatorsFromAssembly(assembly);

        // Scoring
        services.AddSingleton<IContentScorer, ContentScorer>();

        return services;
    }
}
