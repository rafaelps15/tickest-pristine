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
        if (command.ResponsibleUserId is { } responsibleUserId)
        {
            bool responsibleUserExists = await context.Users.AnyAsync(u => u.Id == responsibleUserId, cancellationToken);

            if (!responsibleUserExists)
            {
                return Result.Failure<Guid>(UserErrors.NotFound(responsibleUserId));
            }
        }

        var department = Department.Create(command.Name, command.Description, command.ResponsibleUserId);

        context.Departments.Add(department);

        await context.SaveChangesAsync(cancellationToken);

        return department.Id;
    }
}
