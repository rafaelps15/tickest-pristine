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
        bool departmentExists = await context.Departments.AnyAsync(d => d.Id == command.DepartmentId && d.IsActive, cancellationToken);

        if (!departmentExists)
        {
            return Result.Failure<Guid>(DepartmentErrors.NotFound(command.DepartmentId));
        }

        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            DepartmentId = command.DepartmentId,
            Description = command.Description,
            IsActive = true
        };

        sector.Raise(new SectorCreatedDomainEvent(sector.Id));

        context.Sectors.Add(sector);

        await context.SaveChangesAsync(cancellationToken);

        return sector.Id;
    }
}
