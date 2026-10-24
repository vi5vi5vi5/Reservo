using FluentValidation.TestHelper;
using Reservo.Application.Auth;

namespace Reservo.UnitTests.Auth;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Filled_email_and_password_pass()
    {
        _validator.TestValidate(new LoginRequest("student@reservo.example", "anything"))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_email_and_password_fail()
    {
        var result = _validator.TestValidate(new LoginRequest("", ""));

        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Password_rules_are_not_checked_on_login()
    {
        // Слабый пароль всё равно должен получать «неверный email или пароль», а не подсказку о правилах пароля.
        _validator.TestValidate(new LoginRequest("student@reservo.example", "1"))
            .ShouldNotHaveValidationErrorFor(x => x.Password);
    }
}
