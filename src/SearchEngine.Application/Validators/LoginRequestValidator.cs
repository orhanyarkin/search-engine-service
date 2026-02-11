using FluentValidation;
using SearchEngine.Application.DTOs;

namespace SearchEngine.Application.Validators;

/// <summary>
/// Giriş isteği doğrulayıcısı.
/// Kullanıcı adı ve şifre uzunluk/format kontrolü sağlar.
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Kullanıcı adı boş olamaz.")
            .MinimumLength(3)
            .WithMessage("Kullanıcı adı en az 3 karakter olmalıdır.")
            .MaximumLength(50)
            .WithMessage("Kullanıcı adı en fazla 50 karakter olabilir.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Şifre boş olamaz.")
            .MinimumLength(6)
            .WithMessage("Şifre en az 6 karakter olmalıdır.");
    }
}
