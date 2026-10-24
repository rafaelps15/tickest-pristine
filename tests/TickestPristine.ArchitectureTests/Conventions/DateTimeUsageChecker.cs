using System.Text.RegularExpressions;

namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Procura o relógio do sistema usado fora do <c>IDateTimeProvider</c>: <c>DateTime.UtcNow</c>, <c>DateTime.Now</c>,
/// <c>DateTime.Today</c>, <c>DateTimeOffset.UtcNow</c> e <c>DateTimeOffset.Now</c> (também com <c>System.</c>) e
/// <c>using static System.DateTime</c>/<c>System.DateTimeOffset</c>, fora de comentários.
/// Limites (busca textual, não semântica): a forma proibida dentro de uma string é reportada; alias
/// (<c>using D = System.DateTime;</c>), reflexão, código gerado e <c>&lt;Using Static="true"&gt;</c> do MSBuild não são detectados.
/// </summary>
internal static partial class DateTimeUsageChecker
{
    public static IReadOnlyList<Violation> Check(IEnumerable<SourceFile> files) =>
        files.SelectMany(Check).ToList();

    public static IReadOnlyList<Violation> Check(SourceFile file)
    {
        string code = CSharpSource.MaskComments(file.Content);

        return Violation.DistinctByLocation(ForbiddenClockRegex()
            .Matches(code)
            .Select(match => new Violation(
                file.Path,
                CSharpSource.GetLine(code, match.Index),
                match.Value,
                "relógio do sistema usado fora do IDateTimeProvider")));
    }

    [GeneratedRegex(
        @"(?<![\w.])(?:using[ \t]+static[ \t]+(?:global::)?System\.DateTime(?:Offset)?[ \t]*;|(?:global::)?(?:System\.)?(?:DateTime\.(?:UtcNow|Now|Today)|DateTimeOffset\.(?:UtcNow|Now))\b)",
        RegexOptions.None,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex ForbiddenClockRegex();
}
