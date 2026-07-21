using FluentValidation;
using TickestPristine.Application.Authorization;

namespace TickestPristine.Application.Roles.AssignPermissions;

internal sealed class AssignRolePermissionsCommandValidator : AbstractValidator<AssignRolePermissionsCommand>
{
    public AssignRolePermissionsCommandValidator()
    {
        RuleFor(c => c.RoleId).NotEmpty();

        RuleFor(c => c.PermissionCodes).NotNull();

        RuleForEach(c => c.PermissionCodes)
            .Must(code => PermissionCodes.All.Contains(code))
            .WithMessage("'{PropertyValue}' is not a known permission code.");
    }
}
