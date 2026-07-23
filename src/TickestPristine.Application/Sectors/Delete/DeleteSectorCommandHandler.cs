using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Sectors;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Sectors.Delete;

internal sealed class DeleteSectorCommandHandler(IApplicationDbContext context)
    : ICommandHandler<DeleteSectorCommand>
{
    public async Task<Result> Handle(DeleteSectorCommand command, CancellationToken cancellationToken)
    {
        Sector? sector = await context.Sectors.SingleOrDefaultAsync(s => s.Id == command.SectorId, cancellationToken);

        if (sector is null)
        {
            return Result.Failure(SectorErrors.NotFound(command.SectorId));
        }

        sector.Deactivate();

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
