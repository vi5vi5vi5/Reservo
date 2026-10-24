using FluentValidation;
using Reservo.Domain.Users;

namespace Reservo.Application.Auth;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Укажите email.")
            .MaximumLength(User.EmailMaxLength).WithMessage($"Email не длиннее {User.EmailMaxLength} символов.")
            .EmailAddress().WithMessage("Email указан в неверном формате.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Укажите пароль.")
            .Length(PasswordMinLength, PasswordMaxLength)
            .WithMessage($"Пароль должен быть от {PasswordMinLength} до {PasswordMaxLength} символов.")
            .Matches("[A-Za-zА-Яа-яЁё]").WithMessage("Пароль должен содержать хотя бы одну букву.")
            .Matches("[0-9]").WithMessage("Пароль должен содержать хотя бы одну цифру.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Укажите имя.")
            .MaximumLength(User.FullNameMaxLength).WithMessage($"Имя не длиннее {User.FullNameMaxLength} символов.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Укажите email.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Укажите пароль.");
    }
}
