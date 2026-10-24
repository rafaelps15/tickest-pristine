using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Departments;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Departments.Create;

internal sealed class CreateDepartmentCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateDepartmentCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateDepartmentCommand command, CancellationToken cancellationToken)
    {
        bool nameInUse = await context.Departments.AnyAsync(d => d.IsActive && d.Name == command.Name, cancellationToken);

        if (nameInUse)
        {
            return Result.Failure<Guid>(DepartmentErrors.NameNotUnique(command.Name));
        }

        Result responsibleCheck = await DepartmentResponsibleUser.EnsureExistsAsync(
            context,
            command.ResponsibleUserId,
            cancellationToken);

        if (responsibleCheck.IsFailure)
        {
            return Result.Failure<Guid>(responsibleCheck.Error);
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
