using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
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

        bool hasActiveTickets = await context.Tickets.AnyAsync(
            t => (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress) &&
                 context.Sectors.Any(s => s.Id == t.SectorId && s.DepartmentId == department.Id),
            cancellationToken);

        if (hasActiveTickets)
        {
            return Result.Failure(DepartmentErrors.HasActiveTickets());
        }

        // Setor não existe sem departamento: os setores ativos são desativados junto.
        List<Sector> activeSectors = await context.Sectors
            .Where(s => s.DepartmentId == department.Id && s.IsActive)
            .ToListAsync(cancellationToken);

        foreach (Sector sector in activeSectors)
        {
            sector.IsActive = false;
            sector.Raise(new SectorDeactivatedDomainEvent(sector.Id));
        }

        department.IsActive = false;

        department.Raise(new DepartmentDeactivatedDomainEvent(department.Id));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
