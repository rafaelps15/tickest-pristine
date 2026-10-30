using TickestPristine.Application.Tickets.GetSlaSummary;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Tickets;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class GetTicketSlaSummaryQueryHandlerTests : BaseHandlerTest
{
    private static readonly DateTime NowUtc = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_CountActiveTicketsBySlaStatus_WhenTicketsExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Sector sector = TicketTestData.CreateSector();
        await SeedTicketAsync(context, sector, TicketStatus.Open, NowUtc.AddHours(-1));
        await SeedTicketAsync(context, sector, TicketStatus.Open, NowUtc.AddHours(-2));
        await SeedTicketAsync(context, sector, TicketStatus.InProgress, NowUtc.AddHours(-3.5));
        await SeedTicketAsync(context, sector, TicketStatus.InProgress, NowUtc.AddHours(-5));
        // Reaberto depois de resolvido: conta como ativo, mas o SLA já foi decidido.
        await SeedTicketAsync(context, sector, TicketStatus.Open, NowUtc.AddHours(-30), NowUtc.AddHours(-28));
        await SeedTicketAsync(context, sector, TicketStatus.Closed, NowUtc.AddHours(-30), NowUtc.AddHours(-20));

        GetTicketSlaSummaryQueryHandler handler = CreateHandler(context);

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
        await using TestDbContext context = CreateDbContext();
        Sector sector = TicketTestData.CreateSector();
        await SeedTicketAsync(context, sector, TicketStatus.Open, NowUtc.AddHours(-5));
        await SeedTicketAsync(context, TicketTestData.CreateSector(), TicketStatus.Open, NowUtc.AddHours(-5));

        GetTicketSlaSummaryQueryHandler handler = CreateHandler(context);

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
        await using TestDbContext context = CreateDbContext();
        // Só chamados encerrados: nenhum entra na contagem dos ativos.
        await SeedTicketAsync(context, TicketTestData.CreateSector(), TicketStatus.Closed, NowUtc.AddHours(-30), NowUtc.AddHours(-20));
        await SeedTicketAsync(context, TicketTestData.CreateSector(), TicketStatus.Canceled, NowUtc.AddHours(-30));

        GetTicketSlaSummaryQueryHandler handler = CreateHandler(context);

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

    private static GetTicketSlaSummaryQueryHandler CreateHandler(TestDbContext context)
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(NowUtc);

        return new GetTicketSlaSummaryQueryHandler(context, dateTimeProvider);
    }

    // Chamado de prioridade Urgente (4 h de SLA, alerta em 3 h) aberto no instante informado.
    private static async Task SeedTicketAsync(
        TestDbContext context,
        Sector sector,
        TicketStatus status,
        DateTime createdAtUtc,
        DateTime? resolvedAtUtc = null)
    {
        // Ativo com data de resolução: resolvido nessa data e reaberto depois.
        bool reopened = Ticket.IsActiveStatus(status) && resolvedAtUtc is not null;

        Ticket ticket = TicketTestData.Create(
            sector: sector,
            status: reopened ? TicketStatus.Resolved : status,
            createdAtUtc: createdAtUtc,
            priority: TicketPriority.Urgent,
            description: "Original description",
            dueAtUtc: TicketSla.DueAt(createdAtUtc, TicketPriority.Urgent),
            slaWarningAtUtc: TicketSla.WarningAt(createdAtUtc, TicketPriority.Urgent),
            resolvedAtUtc: resolvedAtUtc);

        if (reopened)
        {
            ticket.Reopen(ticket.CreatedByUserId, resolvedAtUtc!.Value);
            ticket.ClearDomainEvents();
        }

        context.Tickets.Add(ticket);

        await context.SaveChangesAsync();
    }
}
