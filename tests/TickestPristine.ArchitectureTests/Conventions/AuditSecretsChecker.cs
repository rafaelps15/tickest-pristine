using System.Reflection;
using System.Text.RegularExpressions;

namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Confere que toda propriedade de tipo auditável com nome de segredo (<c>*Hash</c>, <c>*Secret*</c>, <c>*Token*</c>,
/// <c>*Stamp</c>) está entre as exclusões da auditoria do próprio tipo: sem isso, o valor iria para a trilha.
/// </summary>
internal static partial class AuditSecretsChecker
{
    public static IReadOnlyList<string> Check(IEnumerable<Type> auditableTypes, Func<Type, string, bool> isExcluded) =>
        [.. auditableTypes
            .SelectMany(type => type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(property => SecretNameRegex().IsMatch(property.Name) && !isExcluded(type, property.Name))
                .Select(property => $"{type.Name}.{property.Name}: nome de segredo fora das exclusões da auditoria do tipo"))
            .Order(StringComparer.Ordinal)];

    [GeneratedRegex("Hash$|Secret|Token|Stamp$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SecretNameRegex();
}
