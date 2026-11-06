using System.Data;
using Dapper;
using Npgsql;
using NpgsqlTypes;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Infrastructure.Tickets;

/// <summary>
/// Leitura e escrita do plano de SLA pelo Dapper na coluna jsonb, no formato de <see cref="TicketSlaPlanJson"/>, o
/// mesmo do EF.
/// </summary>
internal sealed class TicketSlaPlanTypeHandler : SqlMapper.TypeHandler<TicketSlaPlan>
{
    public override void SetValue(IDbDataParameter parameter, TicketSlaPlan? value)
    {
        parameter.Value = value is null ? DBNull.Value : TicketSlaPlanJson.Serialize(value);

        if (parameter is NpgsqlParameter npgsqlParameter)
        {
            npgsqlParameter.NpgsqlDbType = NpgsqlDbType.Jsonb;
        }
    }

    public override TicketSlaPlan? Parse(object value) => TicketSlaPlanJson.Deserialize((string)value);
}
