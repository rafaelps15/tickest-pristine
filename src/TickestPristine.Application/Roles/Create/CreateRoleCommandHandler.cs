using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Roles.Create;

internal sealed class CreateRoleCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateRoleCommand, RoleResponse>
{
    public async Task<Result<RoleResponse>> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        string name = command.RoleName.Trim();

        // roles.name é citext: a comparação não diferencia maiúsculas. Duas criações simultâneas que passem as duas por
        // aqui param no índice único, e o GlobalExceptionHandler responde 409 à segunda.
        bool nameInUse = await context.Roles.AnyAsync(r => r.Name == name, cancellationToken);

        if (nameInUse)
        {
            return Result.Failure<RoleResponse>(RoleErrors.NameNotUnique(name));
        }

        // Nasce sem permissões; nenhuma é concedida, então não há escalada de privilégio a conferir.
        var role = new Role { Id = Guid.NewGuid(), Name = name };

        role.Raise(new RoleCreatedDomainEvent(role.Id));

        context.Roles.Add(role);

        await context.SaveChangesAsync(cancellationToken);

        return new RoleResponse { Id = role.Id, Name = role.Name };
    }
}
