using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Departments;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Departments.Delete;

internal sealed class DeleteDepartmentCommandHandler(IApplicationDbContext context)
    : ICommandHandler<DeleteDepartmentCommand>
{
    public async Task<Result> Handle(DeleteDepartmentCommand command, CancellationToken cancellationToken)
    {
        Department? department = await context.Departments
            .SingleOrDefaultAsync(d => d.Id == command.DepartmentId, cancellationToken);

        if (department is null)
        {
            return Result.Failure(DepartmentErrors.NotFound(command.DepartmentId));
        }

        department.Deactivate();

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
