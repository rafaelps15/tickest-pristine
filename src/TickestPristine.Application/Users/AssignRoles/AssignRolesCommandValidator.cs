using FluentValidation;

namespace TickestPristine.Application.Users.AssignRoles;

internal sealed class AssignRolesCommandValidator : AbstractValidator<AssignRolesCommand>
{
    public AssignRolesCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();

        RuleFor(c => c.RoleIds).NotNull();
    }
}
