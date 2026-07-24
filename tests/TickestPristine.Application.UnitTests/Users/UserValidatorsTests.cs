using TickestPristine.Application.Users.Login;
using TickestPristine.Application.Users.Refresh;
using TickestPristine.Application.Users.Register;
using FluentValidation.TestHelper;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class UserValidatorsTests
{
    private readonly RegisterUserCommandValidator _registerValidator = new();
    private readonly RefreshTokenCommandValidator _refreshValidator = new();
    private readonly LoginUserCommandValidator _loginValidator = new();

    [Fact]
    public void RegisterValidator_Should_HaveError_WhenEmailIsInvalid()
    {
        var command = new RegisterUserCommand("not-an-email", "First", "Last", "Password123");

        TestValidationResult<RegisterUserCommand> result = _registerValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void RegisterValidator_Should_HaveError_WhenPasswordIsTooShort()
    {
        var command = new RegisterUserCommand("test@example.com", "First", "Last", "short");

        TestValidationResult<RegisterUserCommand> result = _registerValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Password);
    }

    [Fact]
    public void RegisterValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        var command = new RegisterUserCommand("test@example.com", "First", "Last", "Password123");

        TestValidationResult<RegisterUserCommand> result = _registerValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RefreshValidator_Should_HaveError_WhenRefreshTokenIsEmpty()
    {
        var command = new RefreshTokenCommand(string.Empty);

        TestValidationResult<RefreshTokenCommand> result = _refreshValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.RefreshToken);
    }

    [Fact]
    public void RefreshValidator_Should_NotHaveErrors_WhenRefreshTokenIsProvided()
    {
        var command = new RefreshTokenCommand("some-refresh-token");

        TestValidationResult<RefreshTokenCommand> result = _refreshValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void LoginValidator_Should_HaveError_WhenEmailIsInvalid()
    {
        var command = new LoginUserCommand("not-an-email", "Password123");

        TestValidationResult<LoginUserCommand> result = _loginValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void LoginValidator_Should_HaveError_WhenPasswordIsEmpty()
    {
        var command = new LoginUserCommand("test@example.com", string.Empty);

        TestValidationResult<LoginUserCommand> result = _loginValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Password);
    }

    [Fact]
    public void LoginValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        var command = new LoginUserCommand("test@example.com", "Password123");

        TestValidationResult<LoginUserCommand> result = _loginValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
