using Shouldly;
using TickestPristine.ArchitectureTests.Conventions.Fixtures;

namespace TickestPristine.ArchitectureTests.Conventions;

public class RichEntityTests : BaseTest
{
    // Entidades já migradas para o domínio rico; cada migração acrescenta as suas.
    private static readonly Type[] RichEntities = [];

    [Fact]
    public void RichEntities_Should_NotHaveNonPrivateSetters()
    {
        // Arrange
        Type[] types = RichEntities;

        // Act
        IReadOnlyList<string> messages = RichEntitySetterChecker.Check(types);

        // Assert
        messages.ShouldBeEmpty(string.Join(Environment.NewLine, messages));
    }

    [Fact]
    public void Checker_Should_NotReportViolation_When_EntityHasOnlyPrivateSetters()
    {
        // Arrange
        Type[] types = [typeof(RichFixtureEntity)];

        // Act
        IReadOnlyList<string> messages = RichEntitySetterChecker.Check(types);

        // Assert
        messages.ShouldBeEmpty(string.Join(Environment.NewLine, messages));
    }

    [Theory]
    [InlineData(typeof(PublicSetterFixtureEntity))]
    [InlineData(typeof(InitSetterFixtureEntity))]
    [InlineData(typeof(InternalSetterFixtureEntity))]
    public void Checker_Should_ReportViolation_When_EntityHasNonPrivateSetter(Type invalidEntity)
    {
        // Arrange
        Type[] types = [typeof(RichFixtureEntity), invalidEntity];

        // Act
        IReadOnlyList<string> messages = RichEntitySetterChecker.Check(types);

        // Assert
        string message = messages.ShouldHaveSingleItem(string.Join(Environment.NewLine, messages));
        message.ShouldBe($"{invalidEntity.FullName}.Name tem setter não privado: use private set e um método da entidade.");
    }

    [Fact]
    public void Checker_Should_ReportViolation_When_ListedTypeIsNotAnEntity()
    {
        // Arrange
        Type[] types = [typeof(NotAnEntityFixture)];

        // Act
        IReadOnlyList<string> messages = RichEntitySetterChecker.Check(types);

        // Assert
        string message = messages.ShouldHaveSingleItem(string.Join(Environment.NewLine, messages));
        message.ShouldContain("não deriva de Entity");
    }
}
