using Shouldly;
using TickestPristine.ArchitectureTests.Conventions.Fixtures;

namespace TickestPristine.ArchitectureTests.Conventions;

public class CommandValidatorTests : BaseTest
{
    [Fact]
    public void ApplicationCommands_Should_HaveValidator()
    {
        // Arrange
        Type[] types = ApplicationAssembly.GetTypes();

        // Act
        IReadOnlyList<string> messages = CommandValidatorChecker.Check(types);

        // Assert
        messages.ShouldBeEmpty(string.Join(Environment.NewLine, messages));
    }

    [Fact]
    public void Checker_Should_NotReportViolation_When_EveryCommandHasValidator()
    {
        // Arrange
        Type[] types =
        [
            typeof(CommandWithValidator),
            typeof(CommandWithValidatorValidator),
            typeof(CommandWithResponseAndValidator),
            typeof(CommandWithResponseAndValidatorValidator),
            typeof(CommandWithIndirectValidator),
            typeof(FixtureBaseValidator<>),
            typeof(CommandWithIndirectValidatorValidator)
        ];

        // Act
        IReadOnlyList<string> messages = CommandValidatorChecker.Check(types);

        // Assert
        messages.ShouldBeEmpty(string.Join(Environment.NewLine, messages));
    }

    [Fact]
    public void Checker_Should_ReportCommandsWithoutValidator_When_ValidatorIsMissingOrForAnotherCommand()
    {
        // Arrange
        Type[] types =
        [
            typeof(CommandWithValidator),
            typeof(CommandWithValidatorValidator),
            typeof(CommandWithoutValidator),
            typeof(CommandWithForeignValidator),
            typeof(CommandWithForeignValidatorValidator)
        ];

        // Act
        IReadOnlyList<string> messages = CommandValidatorChecker.Check(types);

        // Assert
        messages.Count.ShouldBe(2, string.Join(Environment.NewLine, messages));
        messages.ShouldContain(message => message.StartsWith(
            $"{typeof(CommandWithForeignValidator).FullName} não tem validator", StringComparison.Ordinal));
        messages.ShouldContain(message => message.StartsWith(
            $"{typeof(CommandWithoutValidator).FullName} não tem validator", StringComparison.Ordinal));
    }

    [Fact]
    public void Checker_Should_ReportNoCommandFound_When_SelectionIsEmpty()
    {
        // Arrange
        Type[] types = [typeof(CommandWithValidatorValidator)];

        // Act
        IReadOnlyList<string> messages = CommandValidatorChecker.Check(types);

        // Assert
        messages.ShouldHaveSingleItem().ShouldContain("Nenhum command encontrado");
    }
}
