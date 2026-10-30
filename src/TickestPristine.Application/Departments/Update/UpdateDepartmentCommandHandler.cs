using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Departments.Update;

internal sealed class UpdateDepartmentCommandHandler(IApplicationDbContext context)
    : ICommandHandler<UpdateDepartmentCommand>
{
    public async Task<Result> Handle(UpdateDepartmentCommand command, CancellationToken cancellationToken)
    {
        Department? department = await context.Departments
            .SingleOrDefaultAsync(d => d.Id == command.DepartmentId, cancellationToken);

        if (department is null)
        {
            return Result.Failure(DepartmentErrors.NotFound(command.DepartmentId));
        }

        if (await context.Departments.AnyAsync(
            d => d.Id != department.Id && d.IsActive && d.Name == command.Name,
            cancellationToken))
        {
            return Result.Failure(DepartmentErrors.NameNotUnique(command.Name));
        }

        User? responsibleUser = null;

        if (command.ResponsibleUserId is { } responsibleUserId)
        {
            responsibleUser = await context.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.Id == responsibleUserId, cancellationToken);

            if (responsibleUser is null)
            {
                return Result.Failure(UserErrors.NotFound(responsibleUserId));
            }
        }

        department.Update(command.Name, command.Description, responsibleUser);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
