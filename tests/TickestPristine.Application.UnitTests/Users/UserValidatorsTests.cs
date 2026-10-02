using TickestPristine.Application.Users.AssignRoles;
using TickestPristine.Application.Users.ChangePassword;
using TickestPristine.Application.Users.Deactivate;
using TickestPristine.Application.Users.Login;
using TickestPristine.Application.Users.Refresh;
using TickestPristine.Application.Users.Register;
using TickestPristine.Application.Users.UpdateProfile;
using FluentValidation.TestHelper;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class UserValidatorsTests
{
    private const string StrengthRequirementsMessage =
        "A senha precisa ter pelo menos 6 caracteres, incluindo uma letra maiúscula e um caractere especial (como !, @, # ou $).";

    private readonly RegisterUserCommandValidator _registerValidator = new();
    private readonly RefreshTokenCommandValidator _refreshValidator = new();
    private readonly LoginUserCommandValidator _loginValidator = new();
    private readonly UpdateUserProfileCommandValidator _updateProfileValidator = new();
    private readonly ChangeUserPasswordCommandValidator _changePasswordValidator = new();
    private readonly AssignUserRolesCommandValidator _assignUserRolesValidator = new();
    private readonly DeactivateUserCommandValidator _deactivateValidator = new();

    [Fact]
    public void RegisterValidator_Should_HaveError_WhenEmailIsInvalid()
    {
        // Arrange
        var command = new RegisterUserCommand("not-an-email", "First", "Last", "Password123!");

        // Act
        TestValidationResult<RegisterUserCommand> result = _registerValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void RegisterValidator_Should_HaveError_WhenPasswordIsEmpty()
    {
        // Arrange
        var command = new RegisterUserCommand("test@example.com", "First", "Last", string.Empty);

        // Act
        TestValidationResult<RegisterUserCommand> result = _registerValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Password)
            .WithErrorMessage("Informe a senha.");
    }

    [Fact]
    public void RegisterValidator_Should_HaveError_WhenPasswordIsTooShort()
    {
        // Arrange
        var command = new RegisterUserCommand("test@example.com", "First", "Last", "Ab1#x");

        // Act
        TestValidationResult<RegisterUserCommand> result = _registerValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Password)
            .WithErrorMessage(StrengthRequirementsMessage);
    }

    [Fact]
    public void RegisterValidator_Should_HaveError_WhenPasswordHasNoUppercaseLetter()
    {
        // Arrange
        var command = new RegisterUserCommand("test@example.com", "First", "Last", "password123!");

        // Act
        TestValidationResult<RegisterUserCommand> result = _registerValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Password)
            .WithErrorMessage(StrengthRequirementsMessage);
    }

    [Fact]
    public void RegisterValidator_Should_HaveError_WhenPasswordHasNoSpecialCharacter()
    {
        // Arrange
        var command = new RegisterUserCommand("test@example.com", "First", "Last", "Password123");

        // Act
        TestValidationResult<RegisterUserCommand> result = _registerValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Password)
            .WithErrorMessage(StrengthRequirementsMessage);
    }

    [Fact]
    public void RegisterValidator_Should_NotHaveErrors_WhenPasswordHasExactlySixCharacters()
    {
        // Arrange
        var command = new RegisterUserCommand("test@example.com", "First", "Last", "Abc12#");

        // Act
        TestValidationResult<RegisterUserCommand> result = _registerValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(c => c.Password);
    }

    [Fact]
    public void RegisterValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new RegisterUserCommand("test@example.com", "First", "Last", "Password123!");

        // Act
        TestValidationResult<RegisterUserCommand> result = _registerValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RefreshValidator_Should_HaveError_WhenRefreshTokenIsEmpty()
    {
        // Arrange
        var command = new RefreshTokenCommand(string.Empty);

        // Act
        TestValidationResult<RefreshTokenCommand> result = _refreshValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.RefreshToken);
    }

    [Fact]
    public void RefreshValidator_Should_NotHaveErrors_WhenRefreshTokenIsProvided()
    {
        // Arrange
        var command = new RefreshTokenCommand("some-refresh-token");

        // Act
        TestValidationResult<RefreshTokenCommand> result = _refreshValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void LoginValidator_Should_HaveError_WhenEmailIsInvalid()
    {
        // Arrange
        var command = new LoginUserCommand("not-an-email", "Password123!");

        // Act
        TestValidationResult<LoginUserCommand> result = _loginValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void LoginValidator_Should_HaveError_WhenPasswordIsEmpty()
    {
        // Arrange
        var command = new LoginUserCommand("test@example.com", string.Empty);

        // Act
        TestValidationResult<LoginUserCommand> result = _loginValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Password);
    }

    [Fact]
    public void LoginValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new LoginUserCommand("test@example.com", "Password123!");

        // Act
        TestValidationResult<LoginUserCommand> result = _loginValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateProfileValidator_Should_HaveError_WhenUserIdIsEmpty()
    {
        // Arrange
        var command = new UpdateUserProfileCommand(Guid.Empty, "First", "Last");

        // Act
        TestValidationResult<UpdateUserProfileCommand> result = _updateProfileValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.UserId);
    }

    [Fact]
    public void UpdateProfileValidator_Should_HaveError_WhenFirstNameIsEmpty()
    {
        // Arrange
        var command = new UpdateUserProfileCommand(Guid.NewGuid(), string.Empty, "Last");

        // Act
        TestValidationResult<UpdateUserProfileCommand> result = _updateProfileValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.FirstName);
    }

    [Fact]
    public void UpdateProfileValidator_Should_HaveError_WhenLastNameIsEmpty()
    {
        // Arrange
        var command = new UpdateUserProfileCommand(Guid.NewGuid(), "First", string.Empty);

        // Act
        TestValidationResult<UpdateUserProfileCommand> result = _updateProfileValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.LastName);
    }

    [Fact]
    public void UpdateProfileValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new UpdateUserProfileCommand(Guid.NewGuid(), "First", "Last");

        // Act
        TestValidationResult<UpdateUserProfileCommand> result = _updateProfileValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ChangePasswordValidator_Should_HaveError_WhenCurrentPasswordIsEmpty()
    {
        // Arrange
        var command = new ChangeUserPasswordCommand(string.Empty, "NewPassword123!");

        // Act
        TestValidationResult<ChangeUserPasswordCommand> result = _changePasswordValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.CurrentPassword)
            .WithErrorMessage("Informe a senha atual.");
    }

    [Fact]
    public void ChangePasswordValidator_Should_HaveError_WhenNewPasswordIsTooShort()
    {
        // Arrange
        var command = new ChangeUserPasswordCommand("Current123!", "Ab1!");

        // Act
        TestValidationResult<ChangeUserPasswordCommand> result = _changePasswordValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.NewPassword)
            .WithErrorMessage(StrengthRequirementsMessage);
    }

    [Fact]
    public void ChangePasswordValidator_Should_HaveError_WhenNewPasswordHasNoUppercaseLetter()
    {
        // Arrange
        var command = new ChangeUserPasswordCommand("Current123!", "newpassword123!");

        // Act
        TestValidationResult<ChangeUserPasswordCommand> result = _changePasswordValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.NewPassword)
            .WithErrorMessage(StrengthRequirementsMessage);
    }

    [Fact]
    public void ChangePasswordValidator_Should_HaveError_WhenNewPasswordHasNoSpecialCharacter()
    {
        // Arrange
        var command = new ChangeUserPasswordCommand("Current123!", "NewPassword123");

        // Act
        TestValidationResult<ChangeUserPasswordCommand> result = _changePasswordValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.NewPassword)
            .WithErrorMessage(StrengthRequirementsMessage);
    }

    [Fact]
    public void ChangePasswordValidator_Should_HaveError_WhenNewPasswordEqualsCurrentPassword()
    {
        // Arrange
        var command = new ChangeUserPasswordCommand("Current123!", "Current123!");

        // Act
        TestValidationResult<ChangeUserPasswordCommand> result = _changePasswordValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.NewPassword)
            .WithErrorMessage("A nova senha precisa ser diferente da atual.");
    }

    [Fact]
    public void ChangePasswordValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new ChangeUserPasswordCommand("Current123!", "NewPassword123!");

        // Act
        TestValidationResult<ChangeUserPasswordCommand> result = _changePasswordValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AssignUserRolesValidator_Should_HaveError_WhenUserIdIsEmpty()
    {
        // Arrange
        var command = new AssignUserRolesCommand { UserId = Guid.Empty, RoleIds = [Guid.NewGuid()] };

        // Act
        TestValidationResult<AssignUserRolesCommand> result = _assignUserRolesValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.UserId);
    }

    [Fact]
    public void AssignUserRolesValidator_Should_HaveError_WhenRoleIdsIsNull()
    {
        // Arrange
        var command = new AssignUserRolesCommand { UserId = Guid.NewGuid(), RoleIds = null! };

        // Act
        TestValidationResult<AssignUserRolesCommand> result = _assignUserRolesValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.RoleIds);
    }

    [Fact]
    public void AssignUserRolesValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new AssignUserRolesCommand { UserId = Guid.NewGuid(), RoleIds = [Guid.NewGuid()] };

        // Act
        TestValidationResult<AssignUserRolesCommand> result = _assignUserRolesValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RegisterValidator_Should_HaveError_WhenFirstNameIsEmpty()
    {
        // Arrange
        var command = new RegisterUserCommand("test@example.com", string.Empty, "Last", "Password123!");

        // Act
        TestValidationResult<RegisterUserCommand> result = _registerValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.FirstName);
    }

    [Fact]
    public void RegisterValidator_Should_HaveError_WhenLastNameIsEmpty()
    {
        // Arrange
        var command = new RegisterUserCommand("test@example.com", "First", string.Empty, "Password123!");

        // Act
        TestValidationResult<RegisterUserCommand> result = _registerValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.LastName);
    }

    [Fact]
    public void RegisterValidator_Should_HaveError_WhenEmailIsEmpty()
    {
        // Arrange
        var command = new RegisterUserCommand(string.Empty, "First", "Last", "Password123!");

        // Act
        TestValidationResult<RegisterUserCommand> result = _registerValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void LoginValidator_Should_HaveError_WhenEmailIsEmpty()
    {
        // Arrange
        var command = new LoginUserCommand(string.Empty, "Password123!");

        // Act
        TestValidationResult<LoginUserCommand> result = _loginValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void RegisterValidator_Should_ReturnRequirementsOnce_WhenPasswordBreaksSeveralRules()
    {
        // Arrange
        var command = new RegisterUserCommand("test@example.com", "First", "Last", "abc");

        // Act
        TestValidationResult<RegisterUserCommand> result = _registerValidator.TestValidate(command);

        // Assert
        result.Errors.Where(e => e.PropertyName == nameof(RegisterUserCommand.Password))
            .ShouldHaveSingleItem()
            .ErrorMessage.ShouldBe(StrengthRequirementsMessage);
    }

    [Fact]
    public void ChangePasswordValidator_Should_HaveError_WhenNewPasswordIsEmpty()
    {
        // Arrange
        var command = new ChangeUserPasswordCommand("Current123!", string.Empty);

        // Act
        TestValidationResult<ChangeUserPasswordCommand> result = _changePasswordValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.NewPassword)
            .WithErrorMessage("Informe a nova senha.");
    }

    [Fact]
    public void ChangePasswordValidator_Should_ReturnRequirementsOnce_WhenNewPasswordBreaksSeveralRules()
    {
        // Arrange
        var command = new ChangeUserPasswordCommand("Current123!", "abc");

        // Act
        TestValidationResult<ChangeUserPasswordCommand> result = _changePasswordValidator.TestValidate(command);

        // Assert
        result.Errors.Where(e => e.PropertyName == nameof(ChangeUserPasswordCommand.NewPassword))
            .ShouldHaveSingleItem()
            .ErrorMessage.ShouldBe(StrengthRequirementsMessage);
    }

    [Fact]
    public void DeactivateValidator_Should_HaveError_WhenUserIdIsEmpty()
    {
        // Arrange
        var command = new DeactivateUserCommand(Guid.Empty);

        // Act
        TestValidationResult<DeactivateUserCommand> result = _deactivateValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.UserId);
    }

    [Fact]
    public void DeactivateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new DeactivateUserCommand(Guid.NewGuid());

        // Act
        TestValidationResult<DeactivateUserCommand> result = _deactivateValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
