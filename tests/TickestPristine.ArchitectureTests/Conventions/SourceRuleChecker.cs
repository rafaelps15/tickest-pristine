using System.Text.RegularExpressions;

namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Regras de texto do código de produção que só procuram uma forma proibida, fora de comentários e strings: a
/// exclusão física de chamado, departamento e setor, o <c>SaveChanges</c> síncrono e o <c>Raise</c> fora do Domain dos
/// eventos das entidades ricas. Cada regra devolve uma violação por local.
/// </summary>
internal static partial class SourceRuleChecker
{
    public static IReadOnlyList<Violation> PhysicalDeletes(IEnumerable<SourceFile> files) =>
        Find(files, PhysicalDeleteRegex(), "exclusão física de chamado, departamento ou setor: use a exclusão lógica ou a desativação");

    public static IReadOnlyList<Violation> SynchronousSaves(IEnumerable<SourceFile> files) =>
        Find(files, SynchronousSaveRegex(), "SaveChanges síncrono: use SaveChangesAsync");

    public static IReadOnlyList<Violation> RaisesOutsideDomain(IEnumerable<SourceFile> files, IReadOnlyCollection<string> eventNames) =>
        [.. Find(files, RaiseRegex(), "evento de entidade rica disparado fora do Domain: o método da entidade dispara o próprio evento")
            .Where(violation => eventNames.Contains(RaiseRegex().Match(violation.Value).Groups["event"].Value))];

    private static List<Violation> Find(IEnumerable<SourceFile> files, Regex regex, string reason)
    {
        var violations = new List<Violation>();

        foreach (SourceFile file in files)
        {
            string code = CSharpSource.MaskCommentsAndStrings(file.Content);

            violations.AddRange(regex.Matches(code).Select(match => new Violation(
                file.Path,
                CSharpSource.GetLine(code, match.Index),
                match.Value,
                reason)));
        }

        return [.. Violation.DistinctByLocation(violations)];
    }

    [GeneratedRegex(@"\.\s*(?:Tickets|Departments|Sectors)\s*\.\s*Remove(?:Range)?\s*\(", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PhysicalDeleteRegex();

    [GeneratedRegex(@"\bSaveChanges\s*\(", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SynchronousSaveRegex();

    [GeneratedRegex(@"\bRaise\s*\(\s*new\s+(?<event>\w+)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RaiseRegex();
}
