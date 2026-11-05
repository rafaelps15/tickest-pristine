using System.Text.RegularExpressions;

namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Confere que nenhum command handler grava uma tabela auditável por fora do change tracker, onde o interceptor de
/// auditoria não alcança: SQL que insere, altera ou exclui linhas da tabela, ou <c>ExecuteUpdate</c> e
/// <c>ExecuteDelete</c> sobre o <c>DbSet</c> dela. O handler que registra a auditoria pelo <c>IAuditLog</c> não é
/// reportado. Comentários não contam; o texto das strings conta, porque é nele que fica o SQL.
/// </summary>
internal static partial class AuditCoverageChecker
{
    private static readonly char[] StatementBoundaries = [';', '{', '}'];

    public static IReadOnlyList<Violation> Check(
        IEnumerable<SourceFile> files,
        IReadOnlyCollection<string> tables,
        IReadOnlyCollection<string> dbSets) =>
        [.. files.SelectMany(file => Check(file, tables, dbSets))];

    public static IReadOnlyList<Violation> Check(
        SourceFile file,
        IReadOnlyCollection<string> tables,
        IReadOnlyCollection<string> dbSets)
    {
        string code = CSharpSource.MaskComments(file.Content);

        if (AuditLogRegex().IsMatch(code))
        {
            return [];
        }

        var violations = new List<Violation>();

        foreach (Match match in SqlWriteRegex().Matches(code))
        {
            string table = match.Groups["table"].Value;

            if (tables.Contains(table, StringComparer.OrdinalIgnoreCase))
            {
                violations.Add(new Violation(
                    file.Path,
                    CSharpSource.GetLine(code, match.Index),
                    table,
                    "grava tabela auditável por SQL, fora do change tracker, sem registrar a auditoria"));
            }
        }

        foreach (Match match in BulkWriteRegex().Matches(code))
        {
            int start = code.LastIndexOfAny(StatementBoundaries, match.Index) + 1;
            HashSet<string> members = [.. MemberRegex().Matches(code[start..match.Index]).Select(member => member.Groups["name"].Value)];

            foreach (string dbSet in dbSets.Where(members.Contains))
            {
                violations.Add(new Violation(
                    file.Path,
                    CSharpSource.GetLine(code, match.Index),
                    dbSet,
                    "grava tabela auditável com ExecuteUpdate ou ExecuteDelete, fora do change tracker, sem registrar a auditoria"));
            }
        }

        return Violation.DistinctByLocation(violations);
    }

    [GeneratedRegex(@"\bIAuditLog\b", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AuditLogRegex();

    [GeneratedRegex(
        @"\b(?:INSERT\s+INTO|UPDATE|DELETE\s+FROM)\s+(?:\\?""?public\\?""?\s*\.\s*)?\\?""?(?<table>[a-z_]+)",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex SqlWriteRegex();

    [GeneratedRegex(@"\.Execute(?:Update|Delete)(?:Async)?\s*\(", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BulkWriteRegex();

    [GeneratedRegex(@"\.\s*(?<name>[A-Za-z_]\w*)\b", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MemberRegex();
}
