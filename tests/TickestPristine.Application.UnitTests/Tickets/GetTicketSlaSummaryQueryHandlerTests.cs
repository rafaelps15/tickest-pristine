using TickestPristine.Application.Tickets.GetSlaSummary;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class GetTicketSlaSummaryQueryHandlerTests : BaseHandlerTest
{
    private static readonly DateTime NowUtc = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_CountActiveTicketsBySlaStatus_WhenTicketsExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var sectorId = Guid.NewGuid();
        await SeedTicketAsync(context, sectorId, TicketStatus.Open, NowUtc.AddHours(-1));
        await SeedTicketAsync(context, sectorId, TicketStatus.Open, NowUtc.AddHours(-2));
        await SeedTicketAsync(context, sectorId, TicketStatus.InProgress, NowUtc.AddHours(-3.5));
        await SeedTicketAsync(context, sectorId, TicketStatus.InProgress, NowUtc.AddHours(-5));
        // Reaberto depois de resolvido: conta como ativo, mas o SLA já foi decidido.
        await SeedTicketAsync(context, sectorId, TicketStatus.Open, NowUtc.AddHours(-30), NowUtc.AddHours(-28));
        await SeedTicketAsync(context, sectorId, TicketStatus.Closed, NowUtc.AddHours(-30), NowUtc.AddHours(-20));

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
        var sectorId = Guid.NewGuid();
        await SeedTicketAsync(context, sectorId, TicketStatus.Open, NowUtc.AddHours(-5));
        await SeedTicketAsync(context, Guid.NewGuid(), TicketStatus.Open, NowUtc.AddHours(-5));

        GetTicketSlaSummaryQueryHandler handler = CreateHandler(context);

        // Act
        Result<TicketSlaSummaryResponse> result = await handler.Handle(new GetTicketSlaSummaryQuery(sectorId), CancellationToken.None);

        // Assert
        result.Value.ActiveCount.ShouldBe(1);
        result.Value.BreachedCount.ShouldBe(1);
    }

    private static GetTicketSlaSummaryQueryHandler CreateHandler(TestDbContext context)
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(NowUtc);

        return new GetTicketSlaSummaryQueryHandler(context, dateTimeProvider);
    }

    /// <summary>
    /// Chamado de prioridade Urgente (4 h de SLA, alerta em 3 h) aberto no instante informado.
    /// </summary>
    private static async Task SeedTicketAsync(
        TestDbContext context,
        Guid sectorId,
        TicketStatus status,
        DateTime createdAtUtc,
        DateTime? resolvedAtUtc = null)
    {
        context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "Original description",
            Priority = TicketPriority.Urgent,
            Status = status,
            CreatedByUserId = Guid.NewGuid(),
            SectorId = sectorId,
            CreatedAtUtc = createdAtUtc,
            DueAtUtc = TicketSla.DueAt(createdAtUtc, TicketPriority.Urgent),
            SlaWarningAtUtc = TicketSla.WarningAt(createdAtUtc, TicketPriority.Urgent),
            ResolvedAtUtc = resolvedAtUtc
        });

        await context.SaveChangesAsync();
    }
}
