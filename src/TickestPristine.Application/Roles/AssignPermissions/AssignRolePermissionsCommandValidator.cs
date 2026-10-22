using FluentValidation;
using TickestPristine.Application.Permissions;

namespace TickestPristine.Application.Roles.AssignPermissions;

internal sealed class AssignRolePermissionsCommandValidator : AbstractValidator<AssignRolePermissionsCommand>
{
    public AssignRolePermissionsCommandValidator()
    {
        RuleFor(c => c.RoleName).NotEmpty();

        RuleFor(c => c.PermissionCodes).NotNull();

        RuleForEach(c => c.PermissionCodes)
            .Must(code => PermissionCatalog.Find(code) is not null)
            .WithMessage("'{PropertyValue}' não é um código de permissão conhecido.");
    }
}
