using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Sectors;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Sectors.GetById;

internal sealed class GetSectorByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetSectorByIdQuery, SectorResponse>
{
    public async Task<Result<SectorResponse>> Handle(GetSectorByIdQuery query, CancellationToken cancellationToken)
    {
        SectorResponse? sector = await context.Sectors
            .Where(s => s.Id == query.SectorId && s.IsActive)
            .Select(s => new SectorResponse
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                DepartmentId = s.DepartmentId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (sector is null)
        {
            return Result.Failure<SectorResponse>(SectorErrors.NotFound(query.SectorId));
        }

        return sector;
    }
}
