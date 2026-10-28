using TickestPristine.Application.Tickets;
using TickestPristine.Application.Tickets.ProcessSlaBreaches;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class ProcessSlaBreachesCommandHandlerTests : BaseHandlerTest
{
    // Segunda-feira, 26/10/2026, 08:10 em São Paulo: primeira resposta vence às 08:40 e a resolução às 12:10.
    private static readonly DateTime OpenedAtUtc = new(2026, 10, 26, 11, 10, 0, DateTimeKind.Utc);

    private static ProcessSlaBreachesCommandHandler CreateHandler(TestDbContext context, DateTime nowUtc) =>
        new(context, CreateClock(nowUtc));

    [Fact]
    public async Task Handle_Should_DoNothing_WhenNoTimerIsDue()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketWithSlaAsync(context, Guid.NewGuid(), OpenedAtUtc);

        // Act: 08:39, antes do primeiro prazo.
        Result<ProcessSlaBreachesResponse> result = await CreateHandler(context, OpenedAtUtc.AddMinutes(29))
            .Handle(new ProcessSlaBreachesCommand(200), CancellationToken.None);

        // Assert
        result.Value.ShouldBe(new ProcessSlaBreachesResponse(0, 0));
        (await context.TicketSlaTimers.Where(t => t.TicketId == ticket.Id).ToListAsync())
            .ShouldAllBe(t => t.BreachedOnUtc == null && t.NextActionOnUtc == t.DueOnUtc);
    }

    [Fact]
    public async Task Handle_Should_BreachDueTimersInDeadlineOrder_UpToBatchSize()
    {
        // Arrange: dois chamados vencidos nos dois objetivos.
        await using TestDbContext context = CreateDbContext();
        Ticket older = await SeedTicketWithSlaAsync(context, Guid.NewGuid(), OpenedAtUtc);
        Ticket newer = await SeedTicketWithSlaAsync(context, Guid.NewGuid(), OpenedAtUtc.AddMinutes(1));
        DateTime nowUtc = OpenedAtUtc.AddHours(5);

        // Act
        Result<ProcessSlaBreachesResponse> result = await CreateHandler(context, nowUtc)
            .Handle(new ProcessSlaBreachesCommand(3), CancellationToken.None);

        // Assert: os 3 com o NextActionOnUtc mais antigo.
        result.Value.ShouldBe(new ProcessSlaBreachesResponse(3, 3));

        List<TicketSlaTimer> timers = await context.TicketSlaTimers.ToListAsync();
        TicketSlaTimer Timer(Ticket ticket, SlaMetric metric) => timers.Single(t => t.TicketId == ticket.Id && t.Metric == metric);

        Timer(older, SlaMetric.FirstResponse).BreachedOnUtc.ShouldNotBeNull();
        Timer(newer, SlaMetric.FirstResponse).BreachedOnUtc.ShouldNotBeNull();
        Timer(older, SlaMetric.Resolution).BreachedOnUtc.ShouldNotBeNull();
        Timer(newer, SlaMetric.Resolution).BreachedOnUtc.ShouldBeNull();
        Timer(newer, SlaMetric.Resolution).NextActionOnUtc.ShouldNotBeNull();

        timers.Where(t => t.BreachedOnUtc != null).ShouldAllBe(t => t.BreachedOnUtc == t.DueOnUtc && t.NextActionOnUtc == null);
    }

    [Fact]
    public async Task Handle_Should_BreachTimerCompletedAfterDeadline_AndOnlyDequeueTheOneCompletedOnTime()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketWithSlaAsync(context, Guid.NewGuid(), OpenedAtUtc);
        List<TicketSlaTimer> seeded = await context.TicketSlaTimers.Where(t => t.TicketId == ticket.Id).ToListAsync();

        // A primeira resposta veio às 08:45, depois do prazo; a resolução, às 11:00, dentro dele.
        TicketSlaClock.Complete(seeded.Single(t => t.Metric == SlaMetric.FirstResponse), OpenedAtUtc.AddMinutes(35), SlaCompletionCause.Message);
        TicketSlaTimer resolution = seeded.Single(t => t.Metric == SlaMetric.Resolution);
        resolution.CompletedOnUtc = OpenedAtUtc.AddMinutes(170);
        await context.SaveChangesAsync();

        // Act
        Result<ProcessSlaBreachesResponse> result = await CreateHandler(context, OpenedAtUtc.AddHours(5))
            .Handle(new ProcessSlaBreachesCommand(200), CancellationToken.None);

        // Assert
        result.Value.ShouldBe(new ProcessSlaBreachesResponse(2, 1));

        List<TicketSlaTimer> timers = await context.TicketSlaTimers.Where(t => t.TicketId == ticket.Id).ToListAsync();
        TicketSlaTimer firstResponse = timers.Single(t => t.Metric == SlaMetric.FirstResponse);
        firstResponse.BreachedOnUtc.ShouldBe(firstResponse.DueOnUtc);
        firstResponse.DomainEvents.ShouldContain(e => e is TicketSlaBreachedDomainEvent);

        TicketSlaTimer resolved = timers.Single(t => t.Metric == SlaMetric.Resolution);
        resolved.BreachedOnUtc.ShouldBeNull();
        resolved.NextActionOnUtc.ShouldBeNull();
        resolved.DomainEvents.ShouldBeEmpty();
    }
}
