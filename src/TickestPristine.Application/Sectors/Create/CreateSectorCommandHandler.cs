using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Sectors.Create;

internal sealed class CreateSectorCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateSectorCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateSectorCommand command, CancellationToken cancellationToken)
    {
        Department? department = await context.Departments
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == command.DepartmentId && d.IsActive, cancellationToken);

        if (department is null)
        {
            return Result.Failure<Guid>(DepartmentErrors.NotFound(command.DepartmentId));
        }

        if (await context.Sectors.AnyAsync(
            s => s.DepartmentId == department.Id && s.IsActive && s.Name == command.Name,
            cancellationToken))
        {
            return Result.Failure<Guid>(SectorErrors.NameNotUnique(command.Name));
        }

        var sector = Sector.Create(department, command.Name, command.Description);

        context.Sectors.Add(sector);

        await context.SaveChangesAsync(cancellationToken);

        return sector.Id;
    }
}
