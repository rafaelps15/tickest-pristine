using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Roles;
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

        Role? administratorRole = await context.Roles.SingleOrDefaultAsync(r => r.IsAdministrator, cancellationToken);

        if (administratorRole is not null &&
            await context.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == administratorRole.Id, cancellationToken))
        {
            Result administratorCheck = await AdministratorGuard.EnsureAnotherActiveAdministratorAsync(
                context,
                administratorRole,
                user.Id,
                cancellationToken);

            if (administratorCheck.IsFailure)
            {
                return administratorCheck;
            }
        }

        user.IsActive = false;
        user.DeactivatedAtUtc = dateTimeProvider.UtcNow;

        user.Raise(new UserDeactivatedDomainEvent(user.Id));

        await context.SaveChangesAsync(cancellationToken);

        // Sem as permissões em cache, o access token que o usuário ainda tem deixa de valer na próxima requisição.
        await permissionProvider.InvalidateAsync(user.Id, cancellationToken);

        return Result.Success();
    }
}
