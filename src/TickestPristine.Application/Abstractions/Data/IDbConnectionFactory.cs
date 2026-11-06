using System.Data.Common;

namespace TickestPristine.Application.Abstractions.Data;

/// <summary>
/// Abre conexões com o banco para as consultas em SQL (Dapper), da mesma fonte de conexões do EF; quem chama fecha a
/// conexão com <c>await using</c>. Os commands continuam pelo <see cref="IApplicationDbContext"/>.
/// </summary>
public interface IDbConnectionFactory
{
    ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
