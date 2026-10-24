using Shouldly;
using TickestPristine.ArchitectureTests.Conventions.Fixtures;

namespace TickestPristine.ArchitectureTests.Conventions;

public class HandlerShapeTests : BaseTest
{
    [Fact]
    public void ApplicationHandlers_Should_BeInternalSealed()
    {
        // Arrange
        Type[] types = ApplicationAssembly.GetTypes();

        // Act
        IReadOnlyList<string> messages = HandlerShapeChecker.Check(types);

        // Assert
        messages.ShouldBeEmpty(string.Join(Environment.NewLine, messages));
    }

    [Fact]
    public void Checker_Should_NotReportViolation_When_HandlersAreInternalSealed()
    {
        // Arrange
        Type[] types =
        [
            typeof(InternalSealedCommandHandler),
            typeof(InternalSealedQueryHandler),
            typeof(InternalSealedDomainEventHandler),
            typeof(FixtureDecorators.CommandHandler<>)
        ];

        // Act
        IReadOnlyList<string> messages = HandlerShapeChecker.Check(types);

        // Assert
        messages.ShouldBeEmpty(string.Join(Environment.NewLine, messages));
    }

    [Theory]
    [InlineData(typeof(PublicSealedCommandHandler), "falta internal.")]
    [InlineData(typeof(InternalUnsealedQueryHandler), "falta sealed.")]
    [InlineData(typeof(PublicUnsealedDomainEventHandler), "falta internal e sealed.")]
    public void Checker_Should_ReportViolation_When_HandlerIsNotInternalSealed(Type invalidHandler, string expectedMissing)
    {
        // Arrange
        Type[] types =
        [
            typeof(InternalSealedCommandHandler),
            typeof(InternalSealedQueryHandler),
            typeof(InternalSealedDomainEventHandler),
            invalidHandler
        ];

        // Act
        IReadOnlyList<string> messages = HandlerShapeChecker.Check(types);

        // Assert
        string message = messages.ShouldHaveSingleItem(string.Join(Environment.NewLine, messages));
        message.ShouldBe($"{invalidHandler.FullName} não é internal sealed: {expectedMissing}");
    }

    [Fact]
    public void Checker_Should_ReportEmptyFamily_When_NoDomainEventHandlerIsFound()
    {
        // Arrange
        Type[] types = [typeof(InternalSealedCommandHandler), typeof(InternalSealedQueryHandler)];

        // Act
        IReadOnlyList<string> messages = HandlerShapeChecker.Check(types);

        // Assert
        string message = messages.ShouldHaveSingleItem(string.Join(Environment.NewLine, messages));
        message.ShouldContain("Nenhum tipo encontrado da família handler de evento de domínio");
    }
}
