using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Roles.Create;

internal sealed class CreateRoleCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateRoleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        bool nameTaken = await context.Roles.AnyAsync(r => r.Name == command.Name, cancellationToken);

        if (nameTaken)
        {
            return Result.Failure<Guid>(RoleErrors.NameNotUnique);
        }

        var role = Role.Create(command.Name);

        context.Roles.Add(role);

        await context.SaveChangesAsync(cancellationToken);

        return role.Id;
    }
}
