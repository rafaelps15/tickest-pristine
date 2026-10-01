using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.UpdateProfile;

internal sealed class UpdateUserProfileCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : ICommandHandler<UpdateUserProfileCommand>
{
    public async Task<Result> Handle(UpdateUserProfileCommand command, CancellationToken cancellationToken)
    {
        bool canUpdateUsers = await permissionProvider.IsSelfOrHasPermissionAsync(
            userContext.UserId,
            command.UserId,
            PermissionCodes.Users.Update,
            cancellationToken);

        if (!canUpdateUsers)
        {
            return Result.Failure(UserErrors.Unauthorized());
        }

        User? user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        if (user.FirstName != command.FirstName || user.LastName != command.LastName)
        {
            user.FirstName = command.FirstName;
            user.LastName = command.LastName;

            user.Raise(new UserProfileUpdatedDomainEvent(user.Id, user.FirstName, user.LastName));
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
