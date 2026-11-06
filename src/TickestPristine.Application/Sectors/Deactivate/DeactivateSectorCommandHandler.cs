using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
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

        if (await context.Tickets
            .Where(Ticket.Active)
            .AnyAsync(t => t.SectorId == sector.Id, cancellationToken))
        {
            return Result.Failure(SectorErrors.HasActiveTickets());
        }

        sector.Deactivate();

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
