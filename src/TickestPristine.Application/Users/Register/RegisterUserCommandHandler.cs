using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.Register;

internal sealed class RegisterUserCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher)
    : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        if (await context.Users.AnyAsync(u => u.Email == command.Email, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.EmailNotUnique);
        }

        Guid? requesterRoleId = await context.Roles
            .Where(r => r.Name == RoleNames.Requester)
            .Select(r => (Guid?)r.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (requesterRoleId is null)
        {
            return Result.Failure<Guid>(RoleErrors.RequesterRoleNotConfigured);
        }

        var user = User.Create(command.Email, command.FirstName, command.LastName);

        context.Users.Add(user);
        context.UserCredentials.Add(UserCredential.Create(user.Id, passwordHasher.Hash(command.Password)));
        context.UserRoles.Add(UserRole.Create(user.Id, requesterRoleId.Value));

        await context.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}
