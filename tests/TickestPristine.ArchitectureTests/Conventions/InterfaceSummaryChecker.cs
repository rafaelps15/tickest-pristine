using System.Text.RegularExpressions;

namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Confere que toda interface declarada tem <c>&lt;summary&gt;</c> logo acima, pulando atributos e diretivas
/// <c>#pragma</c> entre o comentário e a declaração. Declarações dentro de comentários e strings não contam.
/// </summary>
internal static partial class InterfaceSummaryChecker
{
    public static IReadOnlyList<Violation> Check(IEnumerable<SourceFile> files) =>
        [.. files.SelectMany(Check)];

    public static IReadOnlyList<Violation> Check(SourceFile file)
    {
        string code = CSharpSource.MaskCommentsAndStrings(file.Content);
        string[] lines = file.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var violations = new List<Violation>();

        foreach (Match match in InterfaceRegex().Matches(code))
        {
            int line = CSharpSource.GetLine(code, match.Index);

            if (!HasSummaryAbove(lines, line - 2))
            {
                violations.Add(new Violation(file.Path, line, match.Groups["name"].Value, "interface sem <summary>"));
            }
        }

        return violations;
    }

    private static bool HasSummaryAbove(string[] lines, int index)
    {
        while (index >= 0)
        {
            string text = lines[index].Trim().TrimStart('﻿');

            if (text.StartsWith('[') || text.StartsWith("#pragma", StringComparison.Ordinal))
            {
                index--;
                continue;
            }

            return text.StartsWith("///", StringComparison.Ordinal) && text.Contains("</summary>", StringComparison.Ordinal);
        }

        return false;
    }

    [GeneratedRegex(
        @"\b(?:public|internal)\s+(?:partial\s+)?interface\s+(?<name>\w+)",
        RegexOptions.None,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex InterfaceRegex();
}
