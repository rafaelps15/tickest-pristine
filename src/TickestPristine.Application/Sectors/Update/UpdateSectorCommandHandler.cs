using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Sectors;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Sectors.Update;

internal sealed class UpdateSectorCommandHandler(IApplicationDbContext context)
    : ICommandHandler<UpdateSectorCommand>
{
    public async Task<Result> Handle(UpdateSectorCommand command, CancellationToken cancellationToken)
    {
        Sector? sector = await context.Sectors.SingleOrDefaultAsync(s => s.Id == command.SectorId, cancellationToken);

        if (sector is null)
        {
            return Result.Failure(SectorErrors.NotFound(command.SectorId));
        }

        if (await context.Sectors.AnyAsync(
            s => s.Id != sector.Id && s.DepartmentId == sector.DepartmentId && s.IsActive && s.Name == command.Name,
            cancellationToken))
        {
            return Result.Failure(SectorErrors.NameNotUnique(command.Name));
        }

        sector.Update(command.Name, command.Description);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
