using Shouldly;

namespace TickestPristine.ArchitectureTests.Conventions;

public class InterfaceSummaryTests : BaseTest
{
    [Fact]
    public void Interfaces_Should_HaveSummary_When_DeclaredInSrc()
    {
        // Arrange
        IReadOnlyList<SourceFile> files = RepositoryFiles.Read("src", name => name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
        files.ShouldNotBeEmpty("Nenhum arquivo lido em src: a seleção pode estar errada.");

        // Act
        IReadOnlyList<Violation> violations = InterfaceSummaryChecker.Check(files);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Theory]
    [InlineData("/// <summary>\n/// Texto.\n/// </summary>\npublic interface IExemplo;")]
    [InlineData("/// <summary>\n/// Texto.\n/// </summary>\n#pragma warning disable CA1040\npublic interface IExemplo;\n#pragma warning restore CA1040")]
    [InlineData("/// <summary>\n/// Texto.\n/// </summary>\n[Obsolete]\ninternal interface IExemplo\n{\n}")]
    [InlineData("// public interface INaoConta;\nvar texto = \"public interface ITambemNao\";")]
    public void Checker_Should_NotReportViolation_When_InterfaceHasSummaryOrIsNotDeclared(string content)
    {
        // Arrange
        var file = new SourceFile("Exemplo.cs", content);

        // Act
        IReadOnlyList<Violation> violations = InterfaceSummaryChecker.Check(file);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Theory]
    [InlineData("public interface IExemplo;")]
    [InlineData("// Comentário comum.\npublic interface IExemplo;")]
    [InlineData("/// <summary>\n/// Texto.\n/// </summary>\n\npublic interface IExemplo;")]
    public void Checker_Should_ReportViolation_When_InterfaceHasNoSummary(string content)
    {
        // Arrange
        var file = new SourceFile("Exemplo.cs", content);

        // Act
        IReadOnlyList<Violation> violations = InterfaceSummaryChecker.Check(file);

        // Assert
        violations.ShouldHaveSingleItem().Value.ShouldBe("IExemplo");
    }
}
