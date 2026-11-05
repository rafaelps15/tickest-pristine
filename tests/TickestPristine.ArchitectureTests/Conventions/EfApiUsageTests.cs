using Shouldly;

namespace TickestPristine.ArchitectureTests.Conventions;

public class EfApiUsageTests : BaseTest
{
    private const string OutsideListPath = "src/TickestPristine.Application/Exemplo/ExemploCommandHandler.cs";

    [Fact]
    public void ApplicationSourceFiles_Should_UseRestrictedEfApisOnlyInAllowedFiles()
    {
        // Arrange
        IReadOnlyList<SourceFile> files = RepositoryFiles.Read(
            "src/TickestPristine.Application", name => name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
        files.ShouldNotBeEmpty("Nenhum arquivo lido em src/TickestPristine.Application: a seleção pode estar errada.");

        // Act
        IReadOnlyList<Violation> violations = EfApiUsageChecker.Check(files, EfApiUsageChecker.AllowedFiles);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Theory]
    [InlineData("context.Entry(user).Property(\"Version\").CurrentValue = 1;", ".Entry(")]
    [InlineData("await context.Tickets.IgnoreQueryFilters().ToListAsync(cancellationToken);", ".IgnoreQueryFilters(")]
    [InlineData("if (entry.State == EntityState.Deleted) { }", "EntityState")]
    [InlineData("context.ChangeTracker.Clear();", "ChangeTracker")]
    [InlineData("catch (DbUpdateConcurrencyException) { }", "DbUpdateConcurrencyException")]
    [InlineData("var pending = context.AuditLogs.Local;", "AuditLogs.Local")]
    public void Checker_Should_ReportViolation_When_RestrictedApiIsUsedOutsideTheList(string statement, string expectedValue)
    {
        // Arrange
        var file = new SourceFile(OutsideListPath, statement);

        // Act
        IReadOnlyList<Violation> violations = EfApiUsageChecker.Check([file], EfApiUsageChecker.AllowedFiles);

        // Assert
        violations.ShouldHaveSingleItem().Value.ShouldBe(expectedValue);
    }

    [Fact]
    public void Checker_Should_NotReportViolation_When_AllowedFileUsesRestrictedApi()
    {
        // Arrange
        var file = new SourceFile(EfApiUsageChecker.AllowedFiles[0], "context.Entry(role).Property(\"Version\").CurrentValue = 1;");

        // Act
        IReadOnlyList<Violation> violations = EfApiUsageChecker.Check([file], EfApiUsageChecker.AllowedFiles);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Fact]
    public void Checker_Should_ReportViolation_When_AllowedFileNoLongerUsesRestrictedApi()
    {
        // Arrange
        var file = new SourceFile(EfApiUsageChecker.AllowedFiles[0], "// context.Entry(role) saiu daqui");

        // Act
        IReadOnlyList<Violation> violations = EfApiUsageChecker.Check([file], EfApiUsageChecker.AllowedFiles);

        // Assert
        violations.ShouldHaveSingleItem().Value.ShouldBe(EfApiUsageChecker.AllowedFiles[0]);
    }
}
