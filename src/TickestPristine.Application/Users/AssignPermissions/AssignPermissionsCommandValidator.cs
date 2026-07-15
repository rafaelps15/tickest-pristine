using FluentValidation;
using TickestPristine.Application.Authorization;

namespace TickestPristine.Application.Users.AssignPermissions;

internal sealed class AssignPermissionsCommandValidator : AbstractValidator<AssignPermissionsCommand>
{
    public AssignPermissionsCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();

        RuleFor(c => c.PermissionCodes).NotNull();

        RuleForEach(c => c.PermissionCodes)
            .Must(code => PermissionCodes.All.Contains(code))
            .WithMessage("'{PropertyValue}' is not a known permission code.");
    }
}
