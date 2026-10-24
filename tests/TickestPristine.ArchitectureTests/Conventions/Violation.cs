namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Violação encontrada por um verificador de texto: arquivo, linha, valor encontrado e motivo.
/// </summary>
internal sealed record Violation(string Path, int Line, string Value, string Reason)
{
    public override string ToString() => $"{Path}:{Line}: {Reason}: {Value}";

    /// <summary>
    /// Reporta cada local (arquivo, linha, valor) uma única vez, mesmo que mais de um padrão o detecte.
    /// </summary>
    public static IReadOnlyList<Violation> DistinctByLocation(IEnumerable<Violation> violations) =>
        violations.DistinctBy(violation => (violation.Path, violation.Line, violation.Value)).ToList();

    public static string Describe(IEnumerable<Violation> violations) =>
        string.Join(Environment.NewLine, violations);
}
