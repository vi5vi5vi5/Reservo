using FluentValidation.TestHelper;
using Reservo.Application.Auth;

namespace Reservo.UnitTests.Auth;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    private static RegisterRequest Valid() => new("student@reservo.example", "Secret123", "Иван Петров");

    [Fact]
    public void Valid_request_passes()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public void Invalid_email_fails(string email)
    {
        _validator.TestValidate(Valid() with { Email = email })
            .ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_longer_than_256_characters_fails()
    {
        var email = new string('a', 250) + "@mail.ru";

        _validator.TestValidate(Valid() with { Email = email })
            .ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("", "Укажите пароль.")]
    [InlineData("Abc1234", "Пароль должен быть от 8 до 128 символов.")]
    [InlineData("12345678", "Пароль должен содержать хотя бы одну букву.")]
    [InlineData("onlyletters", "Пароль должен содержать хотя бы одну цифру.")]
    [InlineData("толькобуквы", "Пароль должен содержать хотя бы одну цифру.")]
    public void Weak_password_fails_with_explanation(string password, string expectedMessage)
    {
        _validator.TestValidate(Valid() with { Password = password })
            .ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage(expectedMessage);
    }

    [Theory]
    [InlineData("Abcdefg1")]
    [InlineData("пароль2026")]
    public void Password_with_letter_and_digit_of_minimum_length_passes(string password)
    {
        _validator.TestValidate(Valid() with { Password = password })
            .ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Password_longer_than_128_characters_fails()
    {
        var password = new string('a', 128) + "1";

        _validator.TestValidate(Valid() with { Password = password })
            .ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_full_name_fails(string fullName)
    {
        _validator.TestValidate(Valid() with { FullName = fullName })
            .ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Fact]
    public void Full_name_longer_than_200_characters_fails()
    {
        _validator.TestValidate(Valid() with { FullName = new string('Я', 201) })
            .ShouldHaveValidationErrorFor(x => x.FullName);
    }
}
