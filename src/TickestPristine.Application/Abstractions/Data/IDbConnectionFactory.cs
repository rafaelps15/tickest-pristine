using System.Data.Common;

namespace TickestPristine.Application.Abstractions.Data;

/// <summary>
/// Conexão aberta para leituras em SQL (Dapper), quando o SQL expressa a consulta melhor que o LINQ, como relatórios
/// agregados. Escritas continuam pelo <see cref="IApplicationDbContext"/>.
/// </summary>
public interface IDbConnectionFactory
{
    ValueTask<DbConnection> OpenConnectionAsync();
}
