using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Sectors.GetAll;

internal sealed class GetSectorsQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetSectorsQuery, List<SectorResponse>>
{
    public async Task<Result<List<SectorResponse>>> Handle(
        GetSectorsQuery request,
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
             WHERE is_active
             ORDER BY name
             """;

        List<SectorResponse> sectors = (await connection.QueryAsync<SectorResponse>(
            new CommandDefinition(sql, cancellationToken: cancellationToken))).AsList();

        return sectors;
    }
}
