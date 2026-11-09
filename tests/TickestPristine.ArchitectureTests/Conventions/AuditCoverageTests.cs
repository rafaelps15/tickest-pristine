using Shouldly;

namespace TickestPristine.ArchitectureTests.Conventions;

public class AuditCoverageTests : BaseTest
{
    [Fact]
    public void Commands_Should_WriteAudit_WhenTheyWriteAuditableTables()
    {
        // Arrange
        IReadOnlyList<SourceFile> files = RepositoryFiles.Read(
            "src/TickestPristine.Application", name => name.EndsWith("CommandHandler.cs", StringComparison.Ordinal));
        files.ShouldNotBeEmpty(
            "Nenhum command handler lido em src/TickestPristine.Application: a seleção dos arquivos pode estar errada.");

        // Act
        IReadOnlyList<Violation> violations = AuditCoverageChecker.Check(files, AuditableModel.Tables, AuditableModel.DbSets);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Theory]
    [InlineData(
        "await context.Database.ExecuteSqlAsync($\"UPDATE public.departments SET name = {command.Name} WHERE id = {command.Id}\", cancellationToken);",
        "departments")]
    [InlineData("await connection.ExecuteAsync(\"delete from tickets where id = @Id\", new { command.Id });", "tickets")]
    [InlineData(
        "await connection.ExecuteAsync(\"INSERT INTO \\\"public\\\".\\\"user_roles\\\" (id) VALUES (@Id)\", new { command.Id });",
        "user_roles")]
    [InlineData(
        "await context.Departments.Where(d => d.Id == command.Id).ExecuteUpdateAsync(s => s.SetProperty(d => d.Name, command.Name), cancellationToken);",
        "Departments")]
    [InlineData("await context.UserRoles\n    .Where(r => r.UserId == command.UserId)\n    .ExecuteDeleteAsync(cancellationToken);", "UserRoles")]
    public void Checker_Should_ReportViolation_When_AuditableTableIsWrittenOutsideChangeTracker(string statement, string expectedValue)
    {
        // Arrange: um command handler de exemplo que grava a tabela sem auditar
        var file = new SourceFile("FixtureCommandHandler.cs", Handler(statement));

        // Act
        IReadOnlyList<Violation> violations = AuditCoverageChecker.Check(file, AuditableModel.Tables, AuditableModel.DbSets);

        // Assert
        violations.ShouldHaveSingleItem().Value.ShouldBe(expectedValue);
    }

    [Theory]
    [InlineData("context.Departments.Add(department);\nawait context.SaveChangesAsync(cancellationToken);")]
    [InlineData("await context.Departments.Where(d => d.IsActive).ToListAsync(cancellationToken);")]
    [InlineData("await context.Database.ExecuteSqlAsync($\"UPDATE public.audit_logs SET user_id = NULL WHERE id = {command.Id}\", cancellationToken);")]
    [InlineData("// await context.Database.ExecuteSqlAsync($\"UPDATE public.departments SET name = 'x'\", cancellationToken);")]
    public void Checker_Should_NotReportViolation_When_WriteGoesThroughChangeTrackerOrTableIsNotAuditable(string statement)
    {
        // Arrange
        var file = new SourceFile("FixtureCommandHandler.cs", Handler(statement));

        // Act
        IReadOnlyList<Violation> violations = AuditCoverageChecker.Check(file, AuditableModel.Tables, AuditableModel.DbSets);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Fact]
    public void Checker_Should_NotReportViolation_When_HandlerRecordsAuditLog()
    {
        // Arrange
        var file = new SourceFile(
            "FixtureCommandHandler.cs",
            Handler(
                "await context.Database.ExecuteSqlAsync($\"UPDATE public.departments SET name = {command.Name}\", cancellationToken);",
                "IApplicationDbContext context, IAuditLog auditLog"));

        // Act
        IReadOnlyList<Violation> violations = AuditCoverageChecker.Check(file, AuditableModel.Tables, AuditableModel.DbSets);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Fact]
    public void AuditableModel_Should_ListTablesAndDbSetsOfEveryAuditableType()
    {
        // Arrange
        IReadOnlyList<AuditableEntity> entities = AuditableModel.Entities;

        // Act
        string[] withoutDbSet = [.. entities
            .Where(entity => entity.DbSets.Count == 0 && entity.Type.Name != "TicketSlaTimer")
            .Select(entity => entity.Type.Name)];

        // Assert
        entities.Count.ShouldBe(13);
        withoutDbSet.ShouldBeEmpty();
        AuditableModel.Entities.Single(entity => entity.Type.Name == "TicketSlaTimer").DbSets.ShouldBeEmpty();
        AuditableModel.Tables.ShouldContain("departments");
        AuditableModel.DbSets.ShouldContain("Departments");
    }

    private static string Handler(string body, string parameters = "IApplicationDbContext context") =>
        $$"""
        internal sealed class FixtureCommandHandler({{parameters}}) : ICommandHandler<FixtureCommand>
        {
            public async Task<Result> Handle(FixtureCommand command, CancellationToken cancellationToken)
            {
                {{body}}

                return Result.Success();
            }
        }
        """;
}
