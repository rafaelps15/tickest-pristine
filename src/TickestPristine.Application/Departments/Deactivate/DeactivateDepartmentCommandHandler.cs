using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Departments;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Departments.Deactivate;

internal sealed class DeactivateDepartmentCommandHandler(IApplicationDbContext context)
    : ICommandHandler<DeactivateDepartmentCommand>
{
    public async Task<Result> Handle(DeactivateDepartmentCommand command, CancellationToken cancellationToken)
    {
        Department? department = await context.Departments
            .SingleOrDefaultAsync(d => d.Id == command.DepartmentId, cancellationToken);

        if (department is null)
        {
            return Result.Failure(DepartmentErrors.NotFound(command.DepartmentId));
        }

        department.IsActive = false;

        department.Raise(new DepartmentDeactivatedDomainEvent(department.Id));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
