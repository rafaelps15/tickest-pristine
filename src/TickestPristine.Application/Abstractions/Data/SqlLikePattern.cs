namespace TickestPristine.Application.Abstractions.Data;

/// <summary>
/// Padrões de <c>LIKE</c> das buscas em SQL, montados como o EF monta: o termo é procurado literalmente, com <c>%</c>,
/// <c>_</c> e a barra invertida escapados pela barra invertida, o escape padrão do PostgreSQL. Numa coluna citext, o
/// <c>LIKE</c> não diferencia maiúsculas.
/// </summary>
internal static class SqlLikePattern
{
    public static string Containing(string term) =>
        $"%{term.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_")}%";

    /// <summary>
    /// Um padrão de <see cref="Containing"/> por palavra do termo, até <paramref name="maxWords"/> palavras; as demais são
    /// ignoradas, e um termo nulo, vazio ou só com espaços não gera padrão.
    /// </summary>
    public static string[] ForWords(string? term, int maxWords) =>
        [.. (term ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Take(maxWords)
            .Select(Containing)];
}
