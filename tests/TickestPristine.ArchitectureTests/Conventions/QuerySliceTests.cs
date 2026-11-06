using Shouldly;
using TickestPristine.ArchitectureTests.Conventions.Fixtures.QuerySlices.Ef;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;

namespace TickestPristine.ArchitectureTests.Conventions;

public class QuerySliceTests : BaseTest
{
    private const string ApplicationNamespace = "TickestPristine.Application";
    private const string FixturesNamespace = "TickestPristine.ArchitectureTests.Conventions.Fixtures.QuerySlices";

    private static readonly Type[] QueriedEntities = [typeof(Ticket), typeof(User)];

    [Fact]
    public void QuerySlices_Should_NotReadThroughApplicationDbContext()
    {
        // Act
        IReadOnlyList<string> messages = QuerySliceChecker.CheckDataAccess(ApplicationAssembly, ApplicationNamespace);

        // Assert
        messages.ShouldBeEmpty(string.Join(Environment.NewLine, messages));
    }

    [Fact]
    public void Application_Should_NotExtendTicketOrUserQueries()
    {
        // Act
        IReadOnlyList<string> messages = QuerySliceChecker.CheckQueryableExtensions(
            ApplicationAssembly,
            ApplicationNamespace,
            QueriedEntities);

        // Assert
        messages.ShouldBeEmpty(string.Join(Environment.NewLine, messages));
    }

    [Theory]
    [InlineData("TickestPristine.Application.Authorization.TicketAccessGuard")]
    [InlineData("TickestPristine.Application.Tickets.TicketAssigneeEligibility")]
    [InlineData("TickestPristine.Application.Tickets.TicketSlaFilters")]
    public void Application_Should_NotHaveRemovedQueryPiece(string typeName)
    {
        // Act
        Type? type = ApplicationAssembly.GetType(typeName);

        // Assert
        type.ShouldBeNull();
    }

    [Fact]
    public void CheckDataAccess_Should_ReportOnlyTheSliceThatReadsThroughContext_WhenSlicesUseBothWays()
    {
        // Act
        IReadOnlyList<string> messages = QuerySliceChecker.CheckDataAccess(typeof(QuerySliceTests).Assembly, FixturesNamespace);

        // Assert
        messages.ShouldBe([$"{typeof(ReadsWithContextQueryHandler).FullName} lê pelo IApplicationDbContext num slice de query"]);
    }

    [Fact]
    public void CheckDataAccess_Should_ReportSelection_WhenThereIsNoQueryHandler()
    {
        // Act
        IReadOnlyList<string> messages = QuerySliceChecker.CheckDataAccess(typeof(QuerySliceTests).Assembly, "Namespace.Inexistente");

        // Assert
        messages.ShouldHaveSingleItem().ShouldStartWith("Nenhum query handler encontrado");
    }

    [Fact]
    public void CheckQueryableExtensions_Should_ReportExtensionOfTicketQuery()
    {
        // Act
        IReadOnlyList<string> messages = QuerySliceChecker.CheckQueryableExtensions(
            typeof(QuerySliceTests).Assembly,
            FixturesNamespace,
            QueriedEntities);

        // Assert
        messages.ShouldBe([$"{typeof(FixtureTicketQueryExtensions).FullName}.{nameof(FixtureTicketQueryExtensions.WhereFixture)} estende consultas do EF"]);
    }
}
