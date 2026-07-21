using TickestPristine.Application.Users.AssignRoles;
using FluentValidation.TestHelper;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class AssignRolesCommandValidatorTests
{
    private readonly AssignRolesCommandValidator _validator = new();

    [Fact]
    public void Should_HaveError_WhenUserIdIsEmpty()
    {
        var command = new AssignRolesCommand { UserId = Guid.Empty, RoleIds = [Guid.NewGuid()] };

        TestValidationResult<AssignRolesCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.UserId);
    }

    [Fact]
    public void Should_NotHaveErrors_WhenCommandIsValid()
    {
        var command = new AssignRolesCommand { UserId = Guid.NewGuid(), RoleIds = [Guid.NewGuid()] };

        TestValidationResult<AssignRolesCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
