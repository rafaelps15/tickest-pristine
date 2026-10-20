using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets;
using TickestPristine.Domain.Sectors;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Sectors.Deactivate;

internal sealed class DeactivateSectorCommandHandler(IApplicationDbContext context)
    : ICommandHandler<DeactivateSectorCommand>
{
    public async Task<Result> Handle(DeactivateSectorCommand command, CancellationToken cancellationToken)
    {
        Sector? sector = await context.Sectors.SingleOrDefaultAsync(s => s.Id == command.SectorId, cancellationToken);

        if (sector is null)
        {
            return Result.Failure(SectorErrors.NotFound(command.SectorId));
        }

        if (!sector.IsActive)
        {
            return Result.Failure(SectorErrors.AlreadyDeactivated(sector.Id));
        }

        bool hasActiveTickets = await context.Tickets
            .WhereActive()
            .AnyAsync(t => t.SectorId == sector.Id, cancellationToken);

        if (hasActiveTickets)
        {
            return Result.Failure(SectorErrors.HasActiveTickets());
        }

        sector.IsActive = false;

        sector.Raise(new SectorDeactivatedDomainEvent(sector.Id));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
