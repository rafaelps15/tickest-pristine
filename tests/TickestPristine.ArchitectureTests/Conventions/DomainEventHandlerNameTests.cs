using Shouldly;
using TickestPristine.ArchitectureTests.Conventions.Fixtures;
using TickestPristine.ArchitectureTests.Conventions.Fixtures.FixtureHappened;

namespace TickestPristine.ArchitectureTests.Conventions;

public class DomainEventHandlerNameTests : BaseTest
{
    [Fact]
    public void EventHandlers_Should_FollowActionOnEventName_When_InEventFolder()
    {
        // Arrange
        Type[] types = ApplicationAssembly.GetTypes();

        // Act
        IReadOnlyList<string> violations = DomainEventHandlerNameChecker.Check(types);

        // Assert
        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Checker_Should_NotReportViolation_When_HandlerFollowsNameAndFolder()
    {
        // Arrange
        Type[] types = [typeof(NotifyOnFixtureHappenedDomainEventHandler)];

        // Act
        IReadOnlyList<string> violations = DomainEventHandlerNameChecker.Check(types);

        // Assert
        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Checker_Should_ReportViolation_When_NameOrFolderIsWrong()
    {
        // Arrange
        Type[] types = [typeof(FixtureHappenedHandler), typeof(LogOnFixtureHappenedDomainEventHandler)];

        // Act
        IReadOnlyList<string> violations = DomainEventHandlerNameChecker.Check(types);

        // Assert
        violations.Count.ShouldBe(3);
        violations.ShouldContain(violation => violation.Contains(nameof(FixtureHappenedHandler), StringComparison.Ordinal) && violation.Contains("o nome deve ser", StringComparison.Ordinal));
        violations.ShouldContain(violation => violation.Contains(nameof(LogOnFixtureHappenedDomainEventHandler), StringComparison.Ordinal) && violation.Contains("namespace", StringComparison.Ordinal));
    }
}
