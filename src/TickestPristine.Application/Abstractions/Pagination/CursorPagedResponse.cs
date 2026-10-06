namespace TickestPristine.Application.Abstractions.Pagination;

/// <summary>
/// Uma página de uma listagem paginada por cursor composto (data + id), do mais novo para o mais antigo. Para pedir a
/// próxima, o cliente envia <see cref="NextCursorDate"/> e <see cref="NextCursorId"/> como cursorDate e cursorId. Não
/// tem total, porque a contagem custaria mais que a página.
/// </summary>
public sealed record CursorPagedResponse<T>(
    List<T> Items,
    DateTime? NextCursorDate,
    Guid? NextCursorId,
    bool HasNextPage);
