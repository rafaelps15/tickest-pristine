using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.Deactivate;

internal sealed class DeactivateUserCommandHandler(
    IApplicationDbContext context,
    IPermissionProvider permissionProvider)
    : ICommandHandler<DeactivateUserCommand>
{
    public async Task<Result> Handle(DeactivateUserCommand command, CancellationToken cancellationToken)
    {
        User? user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        if (!user.IsActive)
        {
            return Result.Failure(UserErrors.AlreadyDeactivated);
        }

        user.IsActive = false;

        user.Raise(new UserDeactivatedDomainEvent(user.Id));

        await context.SaveChangesAsync(cancellationToken);

        // Sem as permissões em cache, o access token que o usuário ainda tem deixa de valer na próxima requisição.
        await permissionProvider.InvalidateAsync(user.Id, cancellationToken);

        return Result.Success();
    }
}
