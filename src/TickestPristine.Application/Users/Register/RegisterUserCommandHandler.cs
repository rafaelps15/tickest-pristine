using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.Register;

internal sealed class RegisterUserCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RegisterUserCommand, Guid>
{
    private static readonly string[] DefaultPermissions =
    [
        PermissionCodes.Tickets.Create,
        PermissionCodes.Tickets.ViewOwn,
        PermissionCodes.Tickets.UpdateOwn,
        PermissionCodes.Users.Access
    ];

    public async Task<Result<Guid>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        if (await context.Users.AnyAsync(u => u.Email == command.Email, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.EmailNotUnique);
        }

        var user = User.Create(command.Email, command.FirstName, command.LastName, dateTimeProvider.UtcNow);

        context.Users.Add(user);
        context.UserCredentials.Add(UserCredential.Create(user.Id, passwordHasher.Hash(command.Password)));

        foreach (string permissionCode in DefaultPermissions)
        {
            context.UserPermissions.Add(UserPermission.Create(user.Id, permissionCode));
        }

        await context.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}
