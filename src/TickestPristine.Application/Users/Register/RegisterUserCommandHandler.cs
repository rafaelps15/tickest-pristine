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
    IPasswordHasher passwordHasher)
    : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        if (await context.Users.AnyAsync(u => u.Email == command.Email, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.EmailNotUnique);
        }

        Guid memberRoleId = await context.Roles
            .Where(r => r.Name == RoleNames.Member)
            .Select(r => r.Id)
            .SingleAsync(cancellationToken);

        var user = User.Create(command.Email, command.FirstName, command.LastName);

        context.Users.Add(user);
        context.UserCredentials.Add(UserCredential.Create(user.Id, passwordHasher.Hash(command.Password)));
        context.UserRoles.Add(UserRole.Create(user.Id, memberRoleId));

        await context.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}
