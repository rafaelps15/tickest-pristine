using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TickestPristine.Web.Api.Infrastructure;

internal enum DatabaseFailureKind
{
    ConcurrencyConflict,
    DuplicateRecord,
    TextOutOfRange
}

/// <summary>
/// Classifica a falha do banco que tem resposta própria, venha a <see cref="PostgresException"/> dentro da
/// <see cref="DbUpdateException"/> (EF) ou lançada direto (Dapper, <c>ExecuteSqlAsync</c>). Os nomes de índice e de
/// constraint que mudam a resposta ficam só aqui. Devolve <see langword="null"/> para as demais falhas.
/// </summary>
internal static class DatabaseFailures
{
    private const string ResolutionCycleIndex = "ix_ticket_sla_timers_ticket_id_metric_cycle";
    private const string TextLengthCheckPrefix = "ck_";
    private const string TextLengthCheckSuffix = "_length";

    public static DatabaseFailureKind? Classify(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            return DatabaseFailureKind.ConcurrencyConflict;
        }

        PostgresException? postgres = exception as PostgresException ?? (exception as DbUpdateException)?.InnerException as PostgresException;

        if (postgres is null)
        {
            return null;
        }

        if (IsResolutionCycleConflict(postgres))
        {
            return DatabaseFailureKind.ConcurrencyConflict;
        }

        if (postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return DatabaseFailureKind.DuplicateRecord;
        }

        return IsTextOutOfRange(postgres) ? DatabaseFailureKind.TextOutOfRange : null;
    }

    /// <summary>
    /// Indica se o índice único de ciclo dos cronômetros recusou um novo ciclo de resolução: duas reaberturas simultâneas
    /// do mesmo chamado calculam o mesmo ciclo, e a segunda é um conflito de concorrência, não um registro duplicado. As
    /// demais violações de unicidade seguem o tratamento de registro duplicado.
    /// </summary>
    private static bool IsResolutionCycleConflict(PostgresException exception) =>
        exception.SqlState == PostgresErrorCodes.UniqueViolation &&
        exception.ConstraintName == ResolutionCycleIndex;

    /// <summary>
    /// Indica se o banco recusou um texto fora do tamanho: acima do tamanho da coluna (<c>22001</c>) ou fora de uma check
    /// constraint de tamanho, as das colunas <c>citext</c>, nomeadas <c>ck_{tabela}_{coluna}_length</c> (<c>23514</c>). As
    /// demais check constraints seguem o tratamento padrão.
    /// </summary>
    private static bool IsTextOutOfRange(PostgresException exception) =>
        exception.SqlState == PostgresErrorCodes.StringDataRightTruncation ||
        exception.SqlState == PostgresErrorCodes.CheckViolation &&
        exception.ConstraintName is { } constraint &&
        constraint.StartsWith(TextLengthCheckPrefix, StringComparison.Ordinal) &&
        constraint.EndsWith(TextLengthCheckSuffix, StringComparison.Ordinal);
}
