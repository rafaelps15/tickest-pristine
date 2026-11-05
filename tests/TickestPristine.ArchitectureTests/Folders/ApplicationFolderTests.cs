using System.Reflection;
using System.Runtime.CompilerServices;
using NetArchTest.Rules;
using Shouldly;

namespace TickestPristine.ArchitectureTests.Folders;

public class ApplicationFolderTests : BaseTest
{
    private const string ApplicationNamespace = "TickestPristine.Application";

    // Pastas cujas classes estáticas qualquer outra pasta pode usar.
    private static readonly string[] SharedFolders = ["Abstractions", "Permissions"];

    // Classes estáticas com regra que outras pastas ainda usam, até a migração que as converte: os guards de privilégio e
    // de último administrador viram peças injetadas com o modelo rico de usuários e funções; o de acesso ao chamado, os
    // filtros de chamado ativo e a elegibilidade de quem atende saem com as consultas em SQL.
    private static readonly string[] LegacyCrossFolderStaticClasses =
    [
        "TickestPristine.Application.Authorization.AdministratorGuard",
        "TickestPristine.Application.Authorization.PrivilegeEscalationGuard",
        "TickestPristine.Application.Authorization.TicketAccessGuard",
        "TickestPristine.Application.Tickets.TicketAssigneeEligibility",
        "TickestPristine.Application.Tickets.TicketSlaFilters"
    ];

    [Fact]
    public void ApplicationFolders_Should_NotUseStaticClassesWithRulesOfOtherFolders()
    {
        // Arrange
        Assembly assembly = ApplicationAssembly;

        // Act
        List<string> violations = FindViolations(assembly, ApplicationNamespace, SharedFolders, LegacyCrossFolderStaticClasses);

        // Assert
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void LegacyCrossFolderStaticClasses_Should_StillBeUsedByOtherFolders()
    {
        // Arrange: sem as exceções, cada classe da lista ainda aparece; a que deixou de aparecer sai da lista
        string[] noExceptions = [];

        // Act
        List<string> violations = FindViolations(ApplicationAssembly, ApplicationNamespace, SharedFolders, noExceptions);

        // Assert
        LegacyCrossFolderStaticClasses.ShouldAllBe(name => violations.Any(violation => violation.EndsWith($"usa {name}", StringComparison.Ordinal)));
    }

    [Fact]
    public void Rule_Should_ReportViolation_When_FolderUsesStaticClassWithRuleOfAnotherFolder()
    {
        // Arrange
        Assembly fixtures = typeof(ApplicationFolderTests).Assembly;

        // Act
        List<string> violations = FindViolations(fixtures, Fixtures.FixtureNamespace.Root, ["Shared"], []);

        // Assert
        violations.ShouldBe([$"{typeof(Fixtures.FolderA.UsesOtherFolderRule).FullName} usa {typeof(Fixtures.FolderB.OtherFolderRule).FullName}"]);
    }

    [Fact]
    public void Rule_Should_NotReportViolation_When_StaticClassIsSharedSameFolderOrConstantsOnly()
    {
        // Arrange: a pasta A usa a regra compartilhada, uma regra da própria pasta e as constantes da pasta B; o uso da
        // regra da pasta B (o caso do teste anterior) fica liberado aqui
        Assembly fixtures = typeof(ApplicationFolderTests).Assembly;
        string[] allowed = [typeof(Fixtures.FolderB.OtherFolderRule).FullName!];

        // Act
        List<string> violations = FindViolations(fixtures, Fixtures.FixtureNamespace.Root, ["Shared"], allowed);

        // Assert
        violations.ShouldBeEmpty();
    }

    // Para cada pasta (primeiro nível abaixo da raiz), procura dependência das classes estáticas com regra das outras
    // pastas, fora das compartilhadas e das exceções. Classe estática só com constantes não é regra.
    private static List<string> FindViolations(
        Assembly assembly,
        string rootNamespace,
        IReadOnlyCollection<string> sharedFolders,
        IReadOnlyCollection<string> allowedClasses)
    {
        List<Type> rules = [.. Types.InAssembly(assembly)
            .That()
            .ResideInNamespace(rootNamespace)
            .GetTypes()
            .Where(type => type.IsAbstract && type.IsSealed && !type.IsNested && HasMethods(type))
            .Where(type => !sharedFolders.Contains(FolderOf(type, rootNamespace)))
            .Where(type => !allowedClasses.Contains(type.FullName))];

        string[] folders = [.. Types.InAssembly(assembly)
            .That()
            .ResideInNamespace(rootNamespace)
            .GetTypes()
            .Select(type => FolderOf(type, rootNamespace))
            .Distinct()
            .Order(StringComparer.Ordinal)];

        var violations = new List<string>();

        foreach (string folder in folders)
        {
            foreach (Type rule in rules.Where(rule => FolderOf(rule, rootNamespace) != folder))
            {
                TestResult result = Types.InAssembly(assembly)
                    .That()
                    .ResideInNamespace(folder.Length == 0 ? rootNamespace : $"{rootNamespace}.{folder}")
                    .Should()
                    .NotHaveDependencyOn(rule.FullName)
                    .GetResult();

                // O filtro de namespace do NetArchTest é por prefixo: confere a pasta de cada tipo encontrado.
                violations.AddRange((result.FailingTypes ?? [])
                    .Where(type => FolderOf(type, rootNamespace) == folder)
                    .Select(type => $"{TopLevel(type).FullName} usa {rule.FullName}"));
            }
        }

        return [.. violations.Distinct().Order(StringComparer.Ordinal)];
    }

    private static bool HasMethods(Type type) =>
        type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(method => !method.IsSpecialName && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null);

    private static string FolderOf(Type type, string rootNamespace)
    {
        string ns = TopLevel(type).Namespace ?? string.Empty;

        if (ns.Length <= rootNamespace.Length)
        {
            return string.Empty;
        }

        string rest = ns[(rootNamespace.Length + 1)..];
        int dot = rest.IndexOf('.', StringComparison.Ordinal);

        return dot < 0 ? rest : rest[..dot];
    }

    private static Type TopLevel(Type type)
    {
        while (type.DeclaringType is not null)
        {
            type = type.DeclaringType;
        }

        return type;
    }
}
