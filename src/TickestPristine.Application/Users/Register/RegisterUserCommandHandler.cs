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

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = command.Email,
            FirstName = command.FirstName,
            LastName = command.LastName,
            PasswordHash = passwordHasher.Hash(command.Password),
            CreatedAtUtc = dateTimeProvider.UtcNow
        };

        user.Raise(new UserRegisteredDomainEvent(user.Id));

        context.Users.Add(user);

        foreach (string permissionCode in DefaultPermissions)
        {
            context.UserPermissions.Add(new UserPermission
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                PermissionCode = permissionCode
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}
