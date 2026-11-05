using System.Text.RegularExpressions;

namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Reprova, na Application, as APIs do EF que expõem o change tracker ou contornam o modelo: <c>.Entry(</c>,
/// <c>EntityState</c>, <c>ChangeTracker</c>, <c>IgnoreQueryFilters</c>, <c>DbUpdateConcurrencyException</c> e
/// <c>AuditLogs.Local</c>, fora de comentários e strings. A lista de exceções só diminui: um arquivo dela que deixa de
/// usar essas APIs também é reportado, para sair da lista.
/// </summary>
internal static partial class EfApiUsageChecker
{
    public static readonly string[] AllowedFiles =
    [
        "src/TickestPristine.Application/Authorization/AdministratorGuard.cs",
        "src/TickestPristine.Application/TicketAttachments/TicketAttachmentDeleted/PurgeFileOnTicketAttachmentDeletedDomainEventHandler.cs",
        "src/TickestPristine.Application/TicketMessages/Post/PostTicketMessageCommandHandler.cs"
    ];

    public static IReadOnlyList<Violation> Check(IEnumerable<SourceFile> files, IReadOnlyCollection<string> allowedFiles)
    {
        var violations = new List<Violation>();
        var users = new HashSet<string>(StringComparer.Ordinal);

        foreach (SourceFile file in files)
        {
            string code = CSharpSource.MaskCommentsAndStrings(file.Content);
            Match match = ForbiddenApiRegex().Match(code);

            if (!match.Success)
            {
                continue;
            }

            users.Add(file.Path);

            if (!allowedFiles.Contains(file.Path))
            {
                violations.Add(new Violation(
                    file.Path,
                    CSharpSource.GetLine(code, match.Index),
                    match.Value,
                    "API do EF restrita usada fora da lista de exceções"));
            }
        }

        violations.AddRange(allowedFiles
            .Where(path => files.Any(file => file.Path == path) && !users.Contains(path))
            .Select(path => new Violation(path, 0, path, "arquivo da lista de exceções que já não usa as APIs restritas: tire-o da lista")));

        return violations;
    }

    [GeneratedRegex(
        @"\.\s*Entry\s*\(|\bEntityState\b|\bChangeTracker\b|\.\s*IgnoreQueryFilters\s*\(|\bDbUpdateConcurrencyException\b|\bAuditLogs\s*\.\s*Local\b",
        RegexOptions.None,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex ForbiddenApiRegex();
}
