using FluentValidation.TestHelper;
using Reservo.Application.Users;

namespace Reservo.UnitTests.Users;

public class UserListQueryValidatorTests
{
    private readonly UserListQueryValidator _validator = new();

    [Fact]
    public void Defaults_are_valid()
    {
        _validator.TestValidate(new UserListQuery()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Page_below_one_fails(int page)
    {
        _validator.TestValidate(new UserListQuery { Page = page })
            .ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Page_size_outside_1_to_100_fails(int pageSize)
    {
        _validator.TestValidate(new UserListQuery { PageSize = pageSize })
            .ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Page_size_on_the_boundaries_passes(int pageSize)
    {
        _validator.TestValidate(new UserListQuery { PageSize = pageSize })
            .ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Search_longer_than_100_characters_fails()
    {
        _validator.TestValidate(new UserListQuery { Search = new string('x', 101) })
            .ShouldHaveValidationErrorFor(x => x.Search);
    }
}
