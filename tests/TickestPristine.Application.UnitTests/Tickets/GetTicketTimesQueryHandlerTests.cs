using TickestPristine.Application.SlaCalendars;
using TickestPristine.Application.Tickets;
using TickestPristine.Application.Tickets.GetTimes;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.UnitTests.Tickets;

// Os cronômetros usam o calendário contínuo (24 horas), em que o minuto útil é o minuto do relógio, para os tempos
// do teste serem exatos.
public sealed class GetTicketTimesQueryHandlerTests : BaseHandlerTest
{
    private static readonly DateTime StartUtc = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Sector DefaultSector = TicketTestData.CreateSector();
    private static readonly Guid SectorId = DefaultSector.Id;

    [Fact]
    public async Task Handle_Should_CountOnlyFirstResponsesCompletedByMessage_WhenSomeWereCompletedByResolution()
    {
        // Arrange: seis respondidos por mensagem e um resolvido sem resposta
        await using TestDbContext context = CreateDbContext();
        foreach (int minutes in new[] { 30, 60, 60, 90, 120, 600 })
        {
            await SeedTimerAsync(context, SlaMetric.FirstResponse, 1, minutes, SlaCompletionCause.Message);
        }

        await SeedTimerAsync(context, SlaMetric.FirstResponse, 1, 5, SlaCompletionCause.Resolution);

        GetTicketTimesQueryHandler handler = CreateHandler(context);

        // Act
        Result<TicketTimesResponse> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.MinimumSampleSize.ShouldBe(5);
        result.Value.FirstResponse.SampleSize.ShouldBe(6);
        result.Value.FirstResponse.AverageMinutes.ShouldBe(160);
        result.Value.FirstResponse.MedianMinutes.ShouldBe(75);
    }

    [Fact]
    public async Task Handle_Should_UseOnlyFirstCycleForResolution_WhenTicketWasReopened()
    {
        // Arrange: cinco resolvidos em 60 minutos no ciclo 1; um deles, reaberto, resolvido de novo em 5000
        await using TestDbContext context = CreateDbContext();
        Guid reopenedTicketId = Guid.Empty;
        for (int i = 0; i < 5; i++)
        {
            reopenedTicketId = await SeedTimerAsync(context, SlaMetric.Resolution, 1, 60, SlaCompletionCause.Resolution);
        }

        await SeedTimerAsync(context, SlaMetric.Resolution, 2, 5000, SlaCompletionCause.Resolution, reopenedTicketId);

        GetTicketTimesQueryHandler handler = CreateHandler(context);

        // Act
        Result<TicketTimesResponse> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.Value.Resolution.SampleSize.ShouldBe(5);
        result.Value.Resolution.AverageMinutes.ShouldBe(60);
        result.Value.Resolution.MedianMinutes.ShouldBe(60);
    }

    [Fact]
    public async Task Handle_Should_ReturnOnlySampleSize_WhenSampleIsBelowMinimum()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        for (int i = 0; i < 4; i++)
        {
            await SeedTimerAsync(context, SlaMetric.Resolution, 1, 60, SlaCompletionCause.Resolution);
        }

        GetTicketTimesQueryHandler handler = CreateHandler(context);

        // Act
        Result<TicketTimesResponse> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.Value.Resolution.SampleSize.ShouldBe(4);
        result.Value.Resolution.AverageMinutes.ShouldBeNull();
        result.Value.Resolution.MedianMinutes.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_LeaveStoppedTimersOut_WhenTicketWasCanceled()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedTimerAsync(context, SlaMetric.Resolution, 1, 60, SlaCompletionCause.Resolution);
        Guid canceledId = await SeedTimerAsync(context, SlaMetric.Resolution, 1, 60, null);
        context.TicketSlaTimers.Single(t => t.TicketId == canceledId).StoppedOnUtc = StartUtc.AddMinutes(60);
        await context.SaveChangesAsync();

        GetTicketTimesQueryHandler handler = CreateHandler(context);

        // Act
        Result<TicketTimesResponse> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.Value.Resolution.SampleSize.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_FilterByCompletionDateAndSector_WhenPeriodAndSectorAreGiven()
    {
        // Arrange: concluídos em setembro e em outubro no setor do teste, e em setembro noutro setor
        await using TestDbContext context = CreateDbContext();
        await SeedTimerAsync(context, SlaMetric.FirstResponse, 1, 30, SlaCompletionCause.Message);
        await SeedTimerAsync(context, SlaMetric.FirstResponse, 1, 30, SlaCompletionCause.Message, startUtc: StartUtc.AddMonths(1));
        await SeedTimerAsync(context, SlaMetric.FirstResponse, 1, 30, SlaCompletionCause.Message, sector: TicketTestData.CreateSector());

        GetTicketTimesQueryHandler handler = CreateHandler(context);
        var scope = new TicketScopeFilter([SectorId], null, null, null, false, null, null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        // Act
        Result<TicketTimesResponse> result = await handler.Handle(new GetTicketTimesQuery(scope), CancellationToken.None);

        // Assert
        result.Value.FirstResponse.SampleSize.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidPeriod_WhenStartIsAfterEnd()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        GetTicketTimesQueryHandler handler = CreateHandler(context);
        var scope = new TicketScopeFilter([], null, null, null, false, null, null, new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 1));

        // Act
        Result<TicketTimesResponse> result = await handler.Handle(new GetTicketTimesQuery(scope), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.InvalidPeriod);
    }

    private static GetTicketTimesQueryHandler CreateHandler(TestDbContext context) =>
        new(context, CreateSlaCalendarProvider());

    private static GetTicketTimesQuery CreateQuery() =>
        new(new TicketScopeFilter([], null, null, null, false, null, null, null, null));

    // Chamado (ou o informado) com um cronômetro concluído minutes minutos depois do início, pela
    // causa informada; sem causa, o cronômetro fica em aberto.
    private static async Task<Guid> SeedTimerAsync(
        TestDbContext context,
        SlaMetric metric,
        int cycle,
        int minutes,
        SlaCompletionCause? cause,
        Guid? ticketId = null,
        DateTime? startUtc = null,
        Sector? sector = null)
    {
        DateTime startedAtUtc = startUtc ?? StartUtc;

        if (ticketId is null)
        {
            Ticket ticket = TicketTestData.Create(
                sector: sector ?? DefaultSector,
                status: TicketStatus.Resolved,
                createdAtUtc: startedAtUtc,
                title: "Chamado dos tempos médios",
                description: "Chamado gravado direto no banco",
                dueAtUtc: startedAtUtc.AddHours(8),
                slaWarningAtUtc: startedAtUtc.AddHours(6));
            context.Tickets.Add(ticket);
            ticketId = ticket.Id;
        }

        context.TicketSlaTimers.Add(new TicketSlaTimer
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId.Value,
            Metric = metric,
            Cycle = cycle,
            TargetMinutes = 480,
            CalendarId = SlaCalendarIds.Continuous,
            StartedAtUtc = startedAtUtc,
            WarningOnUtc = startedAtUtc.AddHours(6),
            DueOnUtc = startedAtUtc.AddHours(8),
            CompletedOnUtc = cause is null ? null : startedAtUtc.AddMinutes(minutes),
            CompletionCause = cause
        });
        await context.SaveChangesAsync();

        return ticketId.Value;
    }
}
