using System.Text.RegularExpressions;

namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Confere que toda propriedade validada (<c>RuleFor(x =&gt; x.Propriedade)</c>) tem nome em português: na tabela
/// <c>ValidationDisplayNames</c> ou com <c>.WithName(...)</c> na mesma regra. Sem isso, a mensagem padrão do
/// FluentValidation mostraria o nome da propriedade em inglês.
/// </summary>
internal static partial class ValidationDisplayNameChecker
{
    public static IReadOnlyList<Violation> Check(IEnumerable<SourceFile> files, IReadOnlyCollection<string> displayNames) =>
        [.. files.SelectMany(file => Check(file, displayNames))];

    public static IReadOnlyList<Violation> Check(SourceFile file, IReadOnlyCollection<string> displayNames)
    {
        string code = CSharpSource.MaskComments(file.Content);
        var violations = new List<Violation>();

        foreach (Match match in RuleRegex().Matches(code))
        {
            int end = code.IndexOf(';', match.Index);
            string rule = end < 0 ? code[match.Index..] : code[match.Index..end];
            string property = match.Groups["property"].Value;

            if (!displayNames.Contains(property) && !WithNameRegex().IsMatch(rule))
            {
                violations.Add(new Violation(
                    file.Path,
                    CSharpSource.GetLine(code, match.Index),
                    property,
                    "propriedade validada sem nome em ValidationDisplayNames nem WithName"));
            }
        }

        return violations;
    }

    [GeneratedRegex(@"\bRuleFor(?:Each)?\s*\(\s*\w+\s*=>\s*\w+\s*\.\s*(?<property>\w+)\s*\)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RuleRegex();

    [GeneratedRegex(@"\.\s*WithName\s*\(", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WithNameRegex();
}
