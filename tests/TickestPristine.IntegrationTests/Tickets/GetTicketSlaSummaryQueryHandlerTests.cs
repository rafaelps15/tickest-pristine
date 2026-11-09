using TickestPristine.Application.Tickets.GetSlaSummary;
using TickestPristine.IntegrationTests.Abstractions;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Application.Tickets;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.SlaCalendars;
using NSubstitute;
using TickestPristine.SharedKernel;

namespace TickestPristine.IntegrationTests.Tickets;

public sealed class GetTicketSlaSummaryQueryHandlerTests(IntegrationTestWebAppFactory factory) : BaseQueryHandlerTest(factory)
{
    private static readonly DateTime NowUtc = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_CountActiveTicketsBySlaStatus_WhenTicketsExist()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        Sector sector = QueryTicketTestData.CreateSector();
        await SeedTicketAsync(context, sector, TicketStatus.Open, NowUtc.AddHours(-1));
        await SeedTicketAsync(context, sector, TicketStatus.Open, NowUtc.AddHours(-2));
        await SeedTicketAsync(context, sector, TicketStatus.InProgress, NowUtc.AddHours(-3.5));
        await SeedTicketAsync(context, sector, TicketStatus.InProgress, NowUtc.AddHours(-5));
        // Reaberto depois de resolvido: conta como ativo, mas o SLA já foi decidido.
        await SeedTicketAsync(context, sector, TicketStatus.Open, NowUtc.AddHours(-30), NowUtc.AddHours(-28));
        await SeedTicketAsync(context, sector, TicketStatus.Closed, NowUtc.AddHours(-30), NowUtc.AddHours(-20));

        GetTicketSlaSummaryQueryHandler handler = CreateHandler();

        // Act
        Result<TicketSlaSummaryResponse> result = await handler.Handle(new GetTicketSlaSummaryQuery(null), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ActiveCount.ShouldBe(5);
        result.Value.OnTimeCount.ShouldBe(2);
        result.Value.AtRiskCount.ShouldBe(1);
        result.Value.BreachedCount.ShouldBe(1);
        result.Value.ServerNowUtc.ShouldBe(NowUtc);
    }

    [Fact]
    public async Task Handle_Should_CountOnlySectorTickets_WhenSectorIsInformed()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        Sector sector = QueryTicketTestData.CreateSector();
        await SeedTicketAsync(context, sector, TicketStatus.Open, NowUtc.AddHours(-5));
        await SeedTicketAsync(context, QueryTicketTestData.CreateSector(), TicketStatus.Open, NowUtc.AddHours(-5));

        GetTicketSlaSummaryQueryHandler handler = CreateHandler();

        // Act
        Result<TicketSlaSummaryResponse> result = await handler.Handle(new GetTicketSlaSummaryQuery(sector.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ActiveCount.ShouldBe(1);
        result.Value.BreachedCount.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_ReturnZeroedSummary_WhenThereAreNoActiveTickets()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        // Só chamados encerrados: nenhum entra na contagem dos ativos.
        await SeedTicketAsync(context, QueryTicketTestData.CreateSector(), TicketStatus.Closed, NowUtc.AddHours(-30), NowUtc.AddHours(-20));
        await SeedTicketAsync(context, QueryTicketTestData.CreateSector(), TicketStatus.Canceled, NowUtc.AddHours(-30));

        GetTicketSlaSummaryQueryHandler handler = CreateHandler();

        // Act
        Result<TicketSlaSummaryResponse> result = await handler.Handle(new GetTicketSlaSummaryQuery(null), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ActiveCount.ShouldBe(0);
        result.Value.OnTimeCount.ShouldBe(0);
        result.Value.AtRiskCount.ShouldBe(0);
        result.Value.BreachedCount.ShouldBe(0);
        result.Value.ServerNowUtc.ShouldBe(NowUtc);
    }

    private GetTicketSlaSummaryQueryHandler CreateHandler()
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(NowUtc);

        return new GetTicketSlaSummaryQueryHandler(DbConnectionFactory, dateTimeProvider);
    }

    // Chamado de prioridade Urgente (4 h de SLA, alerta em 3 h) aberto no instante informado.
    private static async Task SeedTicketAsync(
        ApplicationDbContext context,
        Sector sector,
        TicketStatus status,
        DateTime createdAtUtc,
        DateTime? resolvedAtUtc = null)
    {
        // Ativo com data de resolução: resolvido nessa data e reaberto depois.
        bool reopened = status is TicketStatus.Open or TicketStatus.InProgress && resolvedAtUtc is not null;

        Ticket ticket = QueryTicketTestData.Create(
            sector: sector,
            status: reopened ? TicketStatus.Resolved : status,
            createdAtUtc: createdAtUtc,
            priority: TicketPriority.Urgent,
            description: "Original description",
            dueAtUtc: createdAtUtc.AddHours(4),
            slaWarningAtUtc: createdAtUtc.AddHours(3),
            resolvedAtUtc: resolvedAtUtc);

        if (reopened)
        {
            ticket.Reopen(ContinuousSlaCalendar.Create(), lastMessageIsFromAgent: false, ticket.CreatedByUserId, resolvedAtUtc!.Value);
            ticket.ClearDomainEvents();
        }

        context.Tickets.Add(ticket);

        await context.SaveChangesAsync();
    }
}
