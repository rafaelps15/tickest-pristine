using FluentValidation;

namespace TickestPristine.Application.Users.AssignRoles;

public class AssignUserRolesCommandValidator : AbstractValidator<AssignUserRolesCommand>
{
    public AssignUserRolesCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();

        RuleFor(c => c.RoleIds).NotNull();
    }
}
