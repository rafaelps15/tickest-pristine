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

        bool hasActiveTickets = await context.Tickets.AnyAsync(
            t => t.SectorId == sector.Id && (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress),
            cancellationToken);

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
