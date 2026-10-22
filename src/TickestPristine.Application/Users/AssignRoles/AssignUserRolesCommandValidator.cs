using FluentValidation;

using TickestPristine.Application.Roles;

namespace TickestPristine.Application.Users.AssignRoles;

internal sealed class AssignUserRolesCommandValidator : AbstractValidator<AssignUserRolesCommand>
{
    public AssignUserRolesCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();

        RuleFor(c => c.RoleNames).NotNull();
    }
}
