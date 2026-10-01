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

        var department = new Department
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            Description = command.Description,
            IsActive = true,
            ResponsibleUserId = command.ResponsibleUserId
        };

        department.Raise(new DepartmentCreatedDomainEvent(department.Id));

        context.Departments.Add(department);

        await context.SaveChangesAsync(cancellationToken);

        return department.Id;
    }
}
