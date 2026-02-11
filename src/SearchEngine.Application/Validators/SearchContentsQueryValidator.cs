using FluentValidation;
using SearchEngine.Application.Queries;

namespace SearchEngine.Application.Validators;

/// <summary>
/// SearchContentsQuery doğrulayıcısı.
/// Sayfalama, anahtar kelime uzunluğu ve pageSize sınırlamalarını kontrol eder.
/// </summary>
public class SearchContentsQueryValidator : AbstractValidator<SearchContentsQuery>
{
    public SearchContentsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Sayfa numarası 1 veya daha büyük olmalıdır.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("Sayfa boyutu 1 ile 50 arasında olmalıdır.");

        RuleFor(x => x.Keyword)
            .MaximumLength(200)
            .WithMessage("Anahtar kelime en fazla 200 karakter olabilir.")
            .When(x => x.Keyword is not null);
    }
}
