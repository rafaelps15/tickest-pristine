using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Departments.Create;

internal sealed class CreateDepartmentCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateDepartmentCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateDepartmentCommand command, CancellationToken cancellationToken)
    {
        if (await context.Departments.AnyAsync(d => d.IsActive && d.Name == command.Name, cancellationToken))
        {
            return Result.Failure<Guid>(DepartmentErrors.NameNotUnique(command.Name));
        }

        User? responsibleUser = null;

        if (command.ResponsibleUserId is { } responsibleUserId)
        {
            responsibleUser = await context.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.Id == responsibleUserId, cancellationToken);

            if (responsibleUser is null)
            {
                return Result.Failure<Guid>(UserErrors.NotFound(responsibleUserId));
            }
        }

        var department = Department.Create(command.Name, command.Description, responsibleUser);

        context.Departments.Add(department);

        await context.SaveChangesAsync(cancellationToken);

        return department.Id;
    }
}
