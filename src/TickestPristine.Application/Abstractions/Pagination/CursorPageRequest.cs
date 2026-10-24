namespace TickestPristine.Application.Abstractions.Pagination;

/// <summary>
/// Página pedida numa listagem paginada por cursor composto (data + id). No endpoint entra com <c>[AsParameters]</c>
/// e vira os parâmetros <c>cursorDate</c>, <c>cursorId</c> e <c>pageSize</c> da URL; sem cursor, a listagem começa
/// na primeira página. O filtro do cursor é aplicado por
/// <see cref="PaginationExtensions.AfterCursor{T}"/> e o tamanho ausente ou inválido é ajustado por
/// <see cref="PaginationExtensions.ToCursorPagedListAsync{T}"/>.
/// </summary>
public sealed record CursorPageRequest(DateTime? CursorDate, Guid? CursorId, int? PageSize)
{
    /// <summary>
    /// Indica se o cliente mandou o cursor completo (data e id); sem ele, a listagem começa na primeira página.
    /// </summary>
    public bool HasCursor => CursorDate.HasValue && CursorId.HasValue;

    /// <summary>
    /// Data do cursor em UTC, exigência do Npgsql nos parâmetros de timestamptz: a data local é convertida e a sem
    /// fuso é tratada como UTC.
    /// </summary>
    public DateTime CursorDateUtc => CursorDate.GetValueOrDefault() is { Kind: DateTimeKind.Local } local
        ? local.ToUniversalTime()
        : DateTime.SpecifyKind(CursorDate.GetValueOrDefault(), DateTimeKind.Utc);
}
