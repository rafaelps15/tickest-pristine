using System.Reflection;
using Shouldly;

namespace TickestPristine.ArchitectureTests.Conventions;

public class ValidationDisplayNameTests : BaseTest
{
    [Fact]
    public void Validators_Should_NameEveryValidatedProperty_When_RuleIsDeclared()
    {
        // Arrange
        IReadOnlyList<SourceFile> files = RepositoryFiles.Read(
            "src/TickestPristine.Application", name => name.EndsWith("Validator.cs", StringComparison.Ordinal));
        files.ShouldNotBeEmpty("Nenhum validator lido em src/TickestPristine.Application: a seleção pode estar errada.");

        // Act
        IReadOnlyList<Violation> violations = ValidationDisplayNameChecker.Check(files, DisplayNames());

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Fact]
    public void Checker_Should_ReportViolation_When_PropertyHasNoDisplayName()
    {
        // Arrange
        var file = new SourceFile("ExemploValidator.cs", "RuleFor(c => c.Unknown).NotEmpty();");

        // Act
        IReadOnlyList<Violation> violations = ValidationDisplayNameChecker.Check(file, ["Name"]);

        // Assert
        violations.ShouldHaveSingleItem().Value.ShouldBe("Unknown");
    }

    [Theory]
    [InlineData("RuleFor(c => c.Name).NotEmpty();")]
    [InlineData("RuleFor(c => c.Unknown)\n    .NotEmpty()\n    .WithName(\"Desconhecido\");")]
    public void Checker_Should_NotReportViolation_When_PropertyIsNamed(string content)
    {
        // Arrange
        var file = new SourceFile("ExemploValidator.cs", content);

        // Act
        IReadOnlyList<Violation> violations = ValidationDisplayNameChecker.Check(file, ["Name"]);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    // A tabela de nomes é interna à Application: lida por reflexão, sem abrir a classe para os testes.
    private static IReadOnlyCollection<string> DisplayNames()
    {
        Type? type = ApplicationAssembly.GetType("TickestPristine.Application.Abstractions.Behaviors.ValidationDisplayNames");
        type.ShouldNotBeNull();

        object? names = type.GetField("Names", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
        names.ShouldBeOfType<Dictionary<string, string>>();

        return [.. ((Dictionary<string, string>)names).Keys];
    }
}
