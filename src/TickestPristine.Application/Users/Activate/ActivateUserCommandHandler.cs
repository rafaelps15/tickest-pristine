using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.Activate;

internal sealed class ActivateUserCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    PrivilegeEscalationCheck privilegeEscalationCheck)
    : ICommandHandler<ActivateUserCommand>
{
    public async Task<Result> Handle(ActivateUserCommand command, CancellationToken cancellationToken)
    {
        User? user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        if (user.IsActive)
        {
            return Result.Failure(UserErrors.AlreadyActive);
        }

        List<string> missingPermissions = await privilegeEscalationCheck.GetMissingPermissionsToManageAsync(
            userContext.UserId,
            user.Id,
            cancellationToken);

        if (missingPermissions.Count > 0)
        {
            return Result.Failure(UserErrors.CannotManagePrivilegedUser(missingPermissions));
        }

        user.IsActive = true;
        user.DeactivatedAtUtc = null;

        user.Raise(new UserActivatedDomainEvent(user.Id));

        await context.SaveChangesAsync(cancellationToken);

        // O cache guardou a lista vazia de quando o usuário estava inativo; sem invalidar, ele seguiria sem permissões.
        // Depois do commit, a invalidação não pode ser cancelada junto com a requisição.
        await permissionProvider.InvalidateAsync(user.Id, CancellationToken.None);

        return Result.Success();
    }
}
