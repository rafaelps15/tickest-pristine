using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Sectors;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Sectors.GetById;

internal sealed class GetSectorByIdQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetSectorByIdQuery, SectorResponse>
{
    public async Task<Result<SectorResponse>> Handle(
        GetSectorByIdQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            $"""
             SELECT
                 id AS {nameof(SectorResponse.Id)},
                 name AS {nameof(SectorResponse.Name)},
                 description AS {nameof(SectorResponse.Description)},
                 department_id AS {nameof(SectorResponse.DepartmentId)}
             FROM public.sectors
             WHERE id = @SectorId AND is_active
             """;

        SectorResponse? sector = await connection.QuerySingleOrDefaultAsync<SectorResponse>(
            new CommandDefinition(sql, request, cancellationToken: cancellationToken));

        if (sector is null)
        {
            return Result.Failure<SectorResponse>(SectorErrors.NotFound(request.SectorId));
        }

        return sector;
    }
}
