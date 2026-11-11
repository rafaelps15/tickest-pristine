using Shouldly;
using TickestPristine.SharedKernel;

namespace TickestPristine.ArchitectureTests.Conventions;

public class SourceRuleTests : BaseTest
{
    private const string DateTimeProviderPath = "src/TickestPristine.Infrastructure/Time/DateTimeProvider.cs";

    private static readonly string[] RichEntityFolders = ["Department", "Sector", "Ticket"];

    // Arquivos da Application que ainda disparam eventos de entidades anêmicas, até a migração que leva cada evento para
    // o método da entidade (usuários e identidade, funções). A lista só encolhe.
    private static readonly string[] LegacyRaiseFiles =
    [
        "src/TickestPristine.Application/Users/Activate/ActivateUserCommandHandler.cs",
        "src/TickestPristine.Application/Users/ActivateAccount/ActivateUserAccountCommandHandler.cs",
        "src/TickestPristine.Application/Users/AssignRoles/AssignUserRolesCommandHandler.cs",
        "src/TickestPristine.Application/Users/ChangeSector/ChangeUserSectorCommandHandler.cs",
        "src/TickestPristine.Application/Users/Deactivate/DeactivateUserCommandHandler.cs",
        "src/TickestPristine.Application/Users/Logout/LogoutUserCommandHandler.cs",
        "src/TickestPristine.Application/Users/Refresh/RefreshTokenCommandHandler.cs",
        "src/TickestPristine.Application/Users/Register/RegisterUserCommandHandler.cs",
        "src/TickestPristine.Application/Users/UpdateProfile/UpdateUserProfileCommandHandler.cs",
        "src/TickestPristine.Application/Users/UserInvitations.cs",
        "src/TickestPristine.Application/Users/UserPasswords.cs",
        "src/TickestPristine.Application/Users/UserSessions.cs"
    ];

    [Fact]
    public void SourceFiles_Should_UseDateTimeProviderInsteadOfSystemClock_OutsideTheProvider()
    {
        // Arrange
        SourceFile[] files = [.. SourceFiles().Where(file => file.Path != DateTimeProviderPath)];

        // Act
        IReadOnlyList<Violation> violations = DateTimeUsageChecker.Check(files);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Fact]
    public void SourceFiles_Should_NotRaiseRichEntityEvents_OutsideDomain()
    {
        // Arrange
        SourceFile[] files = [.. SourceFiles().Where(file => !file.Path.StartsWith("src/TickestPristine.Domain/", StringComparison.Ordinal))];

        // Act
        IReadOnlyList<Violation> violations = SourceRuleChecker.RaisesOutsideDomain(files, RichEntityEventNames());

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Fact]
    public void SourceFiles_Should_NotRaiseAnyEvent_OutsideDomain_ExceptLegacyFiles()
    {
        // Arrange
        SourceFile[] files = [.. SourceFiles().Where(file =>
            !file.Path.StartsWith("src/TickestPristine.Domain/", StringComparison.Ordinal) && !LegacyRaiseFiles.Contains(file.Path))];

        // Act
        IReadOnlyList<Violation> violations = SourceRuleChecker.RaisesOutsideDomain(files, AllEventNames());

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Fact]
    public void LegacyRaiseFiles_Should_StillRaiseEvents()
    {
        // Arrange: o arquivo que já não dispara evento sai da lista
        SourceFile[] files = [.. SourceFiles().Where(file => LegacyRaiseFiles.Contains(file.Path))];

        // Act
        string[] stillRaising = [.. SourceRuleChecker.RaisesOutsideDomain(files, AllEventNames())
            .Select(violation => violation.Path)
            .Distinct()
            .Order(StringComparer.Ordinal)];

        // Assert
        stillRaising.ShouldBe([.. LegacyRaiseFiles.Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public void SourceFiles_Should_NotDeleteTicketsDepartmentsOrSectorsPhysically()
    {
        // Arrange
        IReadOnlyList<SourceFile> files = SourceFiles();

        // Act
        IReadOnlyList<Violation> violations = SourceRuleChecker.PhysicalDeletes(files);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Fact]
    public void SourceFiles_Should_NotCallSynchronousSaveChanges()
    {
        // Arrange
        IReadOnlyList<SourceFile> files = SourceFiles();

        // Act
        IReadOnlyList<Violation> violations = SourceRuleChecker.SynchronousSaves(files);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Fact]
    public void RichEntityEventNames_Should_IncludeEventsOfEveryRichEntityFolder()
    {
        // Arrange
        IReadOnlyCollection<string> names = RichEntityEventNames();

        // Act
        bool[] found = [.. RichEntityFolders.Select(prefix => names.Any(name => name.StartsWith(prefix, StringComparison.Ordinal)))];

        // Assert
        found.ShouldAllBe(hasEvent => hasEvent);
    }

    [Fact]
    public void Checker_Should_ReportViolation_When_RichEntityEventIsRaisedOutsideDomain()
    {
        // Arrange
        var file = new SourceFile("Exemplo.cs", "ticket.Raise(new TicketClosedDomainEvent(ticket.Id));");

        // Act
        IReadOnlyList<Violation> violations = SourceRuleChecker.RaisesOutsideDomain([file], ["TicketClosedDomainEvent"]);

        // Assert
        violations.ShouldHaveSingleItem().Value.ShouldContain("TicketClosedDomainEvent");
    }

    [Theory]
    [InlineData("user.Raise(new UserActivatedDomainEvent(user.Id));")]
    [InlineData("// ticket.Raise(new TicketClosedDomainEvent(ticket.Id));")]
    public void Checker_Should_NotReportViolation_When_EventIsNotFromRichEntityOrIsComment(string content)
    {
        // Arrange
        var file = new SourceFile("Exemplo.cs", content);

        // Act
        IReadOnlyList<Violation> violations = SourceRuleChecker.RaisesOutsideDomain([file], ["TicketClosedDomainEvent"]);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Theory]
    [InlineData("context.Tickets.Remove(ticket);")]
    [InlineData("context.Departments.RemoveRange(departments);")]
    [InlineData("context.Sectors\n    .Remove(sector);")]
    public void Checker_Should_ReportViolation_When_TicketDepartmentOrSectorIsDeletedPhysically(string content)
    {
        // Arrange
        var file = new SourceFile("Exemplo.cs", content);

        // Act
        IReadOnlyList<Violation> violations = SourceRuleChecker.PhysicalDeletes([file]);

        // Assert
        violations.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("context.TicketMessages.Remove(message);")]
    [InlineData("context.UserRoles.RemoveRange(roles);")]
    [InlineData("// context.Tickets.Remove(ticket);")]
    public void Checker_Should_NotReportViolation_When_DeleteIsAllowedOrIsComment(string content)
    {
        // Arrange
        var file = new SourceFile("Exemplo.cs", content);

        // Act
        IReadOnlyList<Violation> violations = SourceRuleChecker.PhysicalDeletes([file]);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Theory]
    [InlineData("context.SaveChanges();")]
    [InlineData("return base.SaveChanges(acceptAllChangesOnSuccess);")]
    public void Checker_Should_ReportViolation_When_SaveChangesIsSynchronous(string content)
    {
        // Arrange
        var file = new SourceFile("Exemplo.cs", content);

        // Act
        IReadOnlyList<Violation> violations = SourceRuleChecker.SynchronousSaves([file]);

        // Assert
        violations.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("await context.SaveChangesAsync(cancellationToken);")]
    [InlineData("// context.SaveChanges();")]
    public void Checker_Should_NotReportViolation_When_SaveChangesIsAsynchronousOrIsComment(string content)
    {
        // Arrange
        var file = new SourceFile("Exemplo.cs", content);

        // Act
        IReadOnlyList<Violation> violations = SourceRuleChecker.SynchronousSaves([file]);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    private static IReadOnlyList<SourceFile> SourceFiles() =>
        RepositoryFiles.Read("src", name => name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyCollection<string> AllEventNames() =>
        [.. DomainAssembly.GetTypes()
            .Where(type => typeof(IDomainEvent).IsAssignableFrom(type) && !type.IsInterface)
            .Select(type => type.Name)];

    // Eventos declarados nas pastas das entidades já migradas para o domínio rico (a lista do RichEntityTests).
    private static IReadOnlyCollection<string> RichEntityEventNames()
    {
        HashSet<string?> namespaces = [.. RichEntityTests.RichEntities.Select(type => type.Namespace)];

        return [.. DomainAssembly.GetTypes()
            .Where(type => typeof(IDomainEvent).IsAssignableFrom(type) && !type.IsInterface && namespaces.Contains(type.Namespace))
            .Select(type => type.Name)];
    }
}



