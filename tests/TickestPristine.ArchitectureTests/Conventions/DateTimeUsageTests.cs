using Shouldly;

namespace TickestPristine.ArchitectureTests.Conventions;

public class DateTimeUsageTests : BaseTest
{
    [Fact]
    public void ApplicationSourceFiles_Should_UseDateTimeProviderInsteadOfSystemClock()
    {
        // Arrange
        IReadOnlyList<SourceFile> files = RepositoryFiles.Read(
            "src/TickestPristine.Application", name => name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
        files.ShouldNotBeEmpty(
            "Nenhum arquivo .cs lido em src/TickestPristine.Application: a seleção dos arquivos pode estar errada.");

        // Act
        IReadOnlyList<Violation> violations = DateTimeUsageChecker.Check(files);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Theory]
    [InlineData("var t = dateTimeProvider.UtcNow;")]
    [InlineData("// DateTime.UtcNow")]
    [InlineData("var x = 1; // DateTime.Now")]
    [InlineData("/// <remarks>DateTime.UtcNow</remarks>")]
    [InlineData("/* DateTime.Now */")]
    [InlineData("/*\n   DateTime.UtcNow\n   DateTimeOffset.Now\n*/\nvar x = 1;")]
    public void Checker_Should_NotReportViolation_When_SystemClockIsNotUsedInCode(string content)
    {
        // Arrange
        var file = new SourceFile("Exemplo.cs", content);

        // Act
        IReadOnlyList<Violation> violations = DateTimeUsageChecker.Check(file);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Theory]
    [InlineData("var t = DateTime.UtcNow;", 1, "DateTime.UtcNow")]
    [InlineData("var t = DateTime.Now;", 1, "DateTime.Now")]
    [InlineData("var t = DateTime.Today;", 1, "DateTime.Today")]
    [InlineData("var t = DateTimeOffset.UtcNow;", 1, "DateTimeOffset.UtcNow")]
    [InlineData("var t = DateTimeOffset.Now;", 1, "DateTimeOffset.Now")]
    [InlineData("var t = System.DateTime.UtcNow;", 1, "System.DateTime.UtcNow")]
    [InlineData("using static System.DateTime;", 1, "using static System.DateTime;")]
    [InlineData("using static System.DateTimeOffset;", 1, "using static System.DateTimeOffset;")]
    [InlineData("var t = DateTime.UtcNow; // agora", 1, "DateTime.UtcNow")]
    [InlineData("/* x */ var t = DateTime.Now;", 1, "DateTime.Now")]
    [InlineData("/* início\n   do comentário\n*/ var t = DateTime.UtcNow;", 3, "DateTime.UtcNow")]
    [InlineData("var u = \"http://x\"; var t = DateTime.UtcNow;", 1, "DateTime.UtcNow")]
    [InlineData("var c = '\"'; var t = DateTime.Now;", 1, "DateTime.Now")]
    public void Checker_Should_ReportViolation_When_SystemClockIsUsedInCode(
        string content,
        int expectedLine,
        string expectedSnippet)
    {
        // Arrange
        var file = new SourceFile("Exemplo.cs", content);

        // Act
        IReadOnlyList<Violation> violations = DateTimeUsageChecker.Check(file);

        // Assert
        Violation violation = violations.ShouldHaveSingleItem(Violation.Describe(violations));
        violation.ToString().ShouldStartWith($"Exemplo.cs:{expectedLine}:");
        violation.Value.ShouldBe(expectedSnippet);
    }

    [Fact]
    public void Checker_Should_ReportViolation_When_SystemClockIsInsideString()
    {
        // Arrange
        // Limitação documentada (R-1): a busca é textual, então a forma proibida dentro de uma string é reportada.
        var file = new SourceFile("Exemplo.cs", "var s = \"DateTime.UtcNow\";");

        // Act
        IReadOnlyList<Violation> violations = DateTimeUsageChecker.Check(file);

        // Assert
        violations.ShouldHaveSingleItem().Value.ShouldBe("DateTime.UtcNow");
    }
}
