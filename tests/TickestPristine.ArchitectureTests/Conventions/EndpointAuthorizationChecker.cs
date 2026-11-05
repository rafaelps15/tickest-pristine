using System.Text.RegularExpressions;

namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Confere que cada endpoint (classe que implementa <c>IEndpoint</c>) declara a proteção da rota, fora de comentários e
/// strings: <c>.RequireAuthorization()</c>, <c>.HasPermission(...)</c>, <c>.RequireAuthenticatedUser()</c> ou
/// <c>.AllowAnonymous()</c>. A negação por padrão protege a rota esquecida em execução; esta regra a reprova no CI.
/// </summary>
internal static partial class EndpointAuthorizationChecker
{
    public static IReadOnlyList<Violation> Check(IEnumerable<SourceFile> files) =>
        [.. files.SelectMany(EndpointTagChecker.FindEndpoints)
            .Where(endpoint => !ProtectionRegex().IsMatch(endpoint.Body))
            .Select(endpoint => new Violation(
                endpoint.Path,
                endpoint.Line,
                endpoint.Name,
                "rota sem proteção declarada: use RequireAuthorization, HasPermission, RequireAuthenticatedUser ou AllowAnonymous"))];

    [GeneratedRegex(
        @"\.\s*(?:RequireAuthorization|HasPermission|RequireAuthenticatedUser|AllowAnonymous)\s*\(",
        RegexOptions.None,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex ProtectionRegex();
}
