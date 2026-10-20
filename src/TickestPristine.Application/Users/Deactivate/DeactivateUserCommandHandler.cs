using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.Deactivate;

internal sealed class DeactivateUserCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeactivateUserCommand>
{
    public async Task<Result> Handle(DeactivateUserCommand command, CancellationToken cancellationToken)
    {
        if (command.UserId == userContext.UserId)
        {
            return Result.Failure(UserErrors.CannotDeactivateSelf);
        }

        User? user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        if (!user.IsActive)
        {
            return Result.Failure(UserErrors.AlreadyDeactivated);
        }

        Result privilegeCheck = await PrivilegeEscalationGuard.EnsureCallerCanManageUserAsync(
            context,
            permissionProvider,
            userContext.UserId,
            user.Id,
            cancellationToken);

        if (privilegeCheck.IsFailure)
        {
            return privilegeCheck;
        }

        Result administratorCheck = await AdministratorGuard.EnsureNotLastAdministratorAsync(
            context,
            user.Id,
            cancellationToken);

        if (administratorCheck.IsFailure)
        {
            return administratorCheck;
        }

        user.IsActive = false;
        user.DeactivatedAtUtc = dateTimeProvider.UtcNow;

        user.Raise(new UserDeactivatedDomainEvent(user.Id));

        await context.SaveChangesAsync(cancellationToken);

        // Sem as permissões e a situação do usuário em cache, o access token que ele ainda tem deixa de valer na próxima
        // requisição (só users/me e logout continuam aceitando o usuário desativado).
        await permissionProvider.InvalidateAsync(user.Id, cancellationToken);

        return Result.Success();
    }
}
