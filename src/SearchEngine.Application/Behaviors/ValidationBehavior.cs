using FluentValidation;
using MediatR;

namespace SearchEngine.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior'u. Her request'i ilgili FluentValidation validator'ları ile doğrular.
/// Doğrulama hatası varsa ValidationException fırlatır (ExceptionHandlingMiddleware yakalar).
/// </summary>
/// <typeparam name="TRequest">MediatR istek tipi.</typeparam>
/// <typeparam name="TResponse">MediatR yanıt tipi.</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next(cancellationToken);
    }
}
