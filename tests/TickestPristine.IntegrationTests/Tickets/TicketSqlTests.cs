using System.Data.Common;
using Dapper;
using TickestPristine.Application.Tickets;
using TickestPristine.Domain.SlaCalendars;
using TickestPristine.Domain.Tickets;
using TickestPristine.Infrastructure.Database;
using TickestPristine.IntegrationTests.Abstractions;

namespace TickestPristine.IntegrationTests.Tickets;

public sealed class TicketSqlTests(IntegrationTestWebAppFactory factory) : BaseQueryHandlerTest(factory)
{
    private static readonly DateTime OpenedAtUtc = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task IsActive_Should_SelectTheTicketsTheEntityConsidersActive_WhenTicketsAreInEveryStatus()
    {
        // Arrange
        Ticket[] tickets = [.. Enum.GetValues<TicketStatus>()
            .Select(status => QueryTicketTestData.Create(status: status, createdAtUtc: OpenedAtUtc))];

        await using (ApplicationDbContext context = CreateDbContext())
        {
            context.Tickets.AddRange(tickets);
            await context.SaveChangesAsync();
        }

        // Act
        List<Guid> selected = await SelectAsync($"SELECT t.id FROM public.tickets t WHERE {TicketSql.IsActive}");

        // Assert
        selected.ShouldBe(tickets.Where(ticket => ticket.IsActive).Select(ticket => ticket.Id), ignoreOrder: true);
    }

    [Theory]
    [InlineData(TicketSlaStatus.OnTime)]
    [InlineData(TicketSlaStatus.AtRisk)]
    [InlineData(TicketSlaStatus.Breached)]
    [InlineData(TicketSlaStatus.Met)]
    [InlineData(TicketSlaStatus.Missed)]
    [InlineData(TicketSlaStatus.NotApplicable)]
    public async Task SlaStatusFragments_Should_SelectTheTicketsTheEntityPutsInTheStatus_WhenTicketsCoverEveryStatus(
        TicketSlaStatus slaStatus)
    {
        // Arrange: um chamado em cada situação no instante de referência
        DateTime nowUtc = OpenedAtUtc.AddHours(10);
        Ticket[] tickets = TicketsInEverySlaStatus(nowUtc);

        await using (ApplicationDbContext context = CreateDbContext())
        {
            context.Tickets.AddRange(tickets);
            await context.SaveChangesAsync();
        }

        string fragment = slaStatus switch
        {
            TicketSlaStatus.OnTime => TicketSql.IsSlaOnTime,
            TicketSlaStatus.AtRisk => TicketSql.IsSlaAtRisk,
            TicketSlaStatus.Breached => TicketSql.IsSlaBreached,
            TicketSlaStatus.Met => TicketSql.IsSlaMet,
            TicketSlaStatus.Missed => TicketSql.IsSlaMissed,
            _ => TicketSql.IsSlaNotApplicable
        };

        // Act
        List<Guid> selected = await SelectAsync($"SELECT t.id FROM public.tickets t WHERE {fragment}", new { NowUtc = nowUtc });

        // Assert
        Guid[] expected = [.. tickets
            .Where(ticket => Ticket.GetSlaStatus(ticket.Status, ticket.DueAtUtc, ticket.SlaWarningAtUtc, ticket.ResolvedAtUtc, nowUtc) == slaStatus)
            .Select(ticket => ticket.Id)];
        expected.ShouldNotBeEmpty();
        selected.ShouldBe(expected, ignoreOrder: true);
    }

    [Fact]
    public async Task CurrentResolutionSlaStatus_Should_AgreeWithTimerState_WhenCurrentCycleIsInEveryState()
    {
        // Arrange: um chamado por estado do cronômetro de resolução, um sem cronômetro (plano legado sem ciclo gravado) e
        // um reaberto, com o ciclo 1 cumprido e o ciclo 2 correndo
        DateTime nowUtc = OpenedAtUtc.AddHours(10);
        var timers = new List<TicketSlaTimer>();
        var tickets = new List<Ticket>();

        void AddTimer(Ticket ticket, int cycle, DateTime warningOnUtc, DateTime dueOnUtc, DateTime? completedOnUtc = null, DateTime? stoppedOnUtc = null)
        {
            TicketSlaTimer timer = new TicketSlaCycleService().Start(ticket, ContinuousSlaCalendar.Create()).Single()
                .With(nameof(TicketSlaTimer.Cycle), cycle)
                .With(nameof(TicketSlaTimer.WarningOnUtc), warningOnUtc)
                .With(nameof(TicketSlaTimer.DueOnUtc), dueOnUtc)
                .With(nameof(TicketSlaTimer.CompletedOnUtc), completedOnUtc)
                .With(nameof(TicketSlaTimer.StoppedOnUtc), stoppedOnUtc);
            timers.Add(timer);
        }

        Ticket NewTicket()
        {
            Ticket ticket = QueryTicketTestData.Create(createdAtUtc: OpenedAtUtc);
            tickets.Add(ticket);

            return ticket;
        }

        AddTimer(NewTicket(), 1, nowUtc.AddHours(1), nowUtc.AddHours(2));
        AddTimer(NewTicket(), 1, nowUtc.AddHours(-1), nowUtc.AddHours(1));
        AddTimer(NewTicket(), 1, nowUtc.AddHours(-2), nowUtc.AddHours(-1));
        AddTimer(NewTicket(), 1, nowUtc.AddHours(-4), nowUtc.AddHours(-3), completedOnUtc: nowUtc.AddHours(-5));
        AddTimer(NewTicket(), 1, nowUtc.AddHours(-4), nowUtc.AddHours(-3), completedOnUtc: nowUtc.AddHours(-2));
        AddTimer(NewTicket(), 1, nowUtc.AddHours(1), nowUtc.AddHours(2), stoppedOnUtc: nowUtc.AddHours(-1));
        Ticket withoutTimer = NewTicket();
        Ticket reopened = NewTicket();
        AddTimer(reopened, 1, nowUtc.AddHours(-4), nowUtc.AddHours(-3), completedOnUtc: nowUtc.AddHours(-5));
        AddTimer(reopened, 2, nowUtc.AddHours(1), nowUtc.AddHours(2));

        await using (ApplicationDbContext context = CreateDbContext())
        {
            context.Tickets.AddRange(tickets);
            context.TicketSlaTimers.AddRange(timers);
            await context.SaveChangesAsync();
        }

        // Act
        await using DbConnection connection = await DbConnectionFactory.OpenConnectionAsync();
        var selected = (await connection.QueryAsync<CurrentStatusRow>(
                $"""
                 SELECT t.id AS {nameof(CurrentStatusRow.Id)}, {TicketSql.CurrentResolutionSlaStatus} AS {nameof(CurrentStatusRow.Status)}
                 FROM public.tickets t
                 {TicketSql.CurrentResolutionTimer}
                 """,
                new { NowUtc = nowUtc }))
            .ToDictionary(row => row.Id, row => row.Status);

        // Assert: a situação do maior ciclo de cada chamado, pela regra do cronômetro; sem cronômetro, nula
        var expected = tickets.ToDictionary(
            ticket => ticket.Id,
            ticket => timers.Where(timer => timer.TicketId == ticket.Id).MaxBy(timer => timer.Cycle) is { } current
                ? ToSlaStatus(current.GetState(nowUtc))
                : (TicketSlaStatus?)null);
        expected[withoutTimer.Id].ShouldBeNull();
        expected[reopened.Id].ShouldBe(TicketSlaStatus.OnTime);
        selected.ShouldBe(expected, ignoreOrder: true);
    }

    [Fact]
    public async Task SlaStatus_Should_AgreeWithTheEntity_WhenTicketsCoverEveryStatus()
    {
        // Arrange: um chamado em cada situação no instante de referência
        DateTime nowUtc = OpenedAtUtc.AddHours(10);
        Ticket[] tickets = TicketsInEverySlaStatus(nowUtc);

        await using (ApplicationDbContext context = CreateDbContext())
        {
            context.Tickets.AddRange(tickets);
            await context.SaveChangesAsync();
        }

        // Act
        await using DbConnection connection = await DbConnectionFactory.OpenConnectionAsync();
        var selected = (await connection.QueryAsync<CurrentStatusRow>(
                $"""
                 SELECT t.id AS {nameof(CurrentStatusRow.Id)}, {TicketSql.SlaStatus} AS {nameof(CurrentStatusRow.Status)}
                 FROM public.tickets t
                 """,
                new { NowUtc = nowUtc }))
            .ToDictionary(row => row.Id, row => row.Status);

        // Assert: a mesma situação que a regra do chamado dá a cada um, e todas as situações aparecem
        Dictionary<Guid, TicketSlaStatus?> expected = tickets.ToDictionary(
            ticket => ticket.Id,
            ticket => (TicketSlaStatus?)Ticket.GetSlaStatus(
                ticket.Status,
                ticket.DueAtUtc,
                ticket.SlaWarningAtUtc,
                ticket.ResolvedAtUtc,
                nowUtc));
        expected.Values.Distinct().Count().ShouldBe(Enum.GetValues<TicketSlaStatus>().Length);
        selected.ShouldBe(expected, ignoreOrder: true);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CanBeCanceled_Should_SelectTheTicketsTheEntityLetsTheCallerCancel_WhenTicketsCoverEveryCase(
        bool canCancelAny,
        bool canEditOwn)
    {
        // Arrange: chamados em cada status, dentro e fora da janela do dono, de quem consulta e de outra pessoa
        DateTime nowUtc = OpenedAtUtc.AddHours(1);
        var callerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        DateTime[] openedAt = [nowUtc.AddMinutes(-5), nowUtc.AddMinutes(-(Ticket.OwnerCancelWindowMinutes + 5))];
        Ticket[] tickets = [.. Enum.GetValues<TicketStatus>()
            .SelectMany(status => openedAt.SelectMany(createdAtUtc => new[] { callerId, otherUserId }
                .Select(requesterId => QueryTicketTestData.Create(requesterId: requesterId, status: status, createdAtUtc: createdAtUtc))))];

        await using (ApplicationDbContext context = CreateDbContext())
        {
            context.Tickets.AddRange(tickets);
            await context.SaveChangesAsync();
        }

        // Act
        List<Guid> selected = await SelectAsync(
            $"SELECT t.id FROM public.tickets t WHERE {TicketSql.CanBeCanceled}",
            new
            {
                NowUtc = nowUtc,
                CallerId = callerId,
                CanCancelAny = canCancelAny,
                CanEditOwn = canEditOwn,
                Ticket.OwnerCancelWindowMinutes
            });

        // Assert
        Guid[] expected = [.. tickets
            .Where(ticket => Ticket.CanBeCanceled(
                ticket.Status,
                ticket.CreatedAtUtc,
                canCancelAny,
                ticket.CreatedByUserId == callerId && canEditOwn,
                nowUtc))
            .Select(ticket => ticket.Id)];
        selected.ShouldBe(expected, ignoreOrder: true);
    }

    [Fact]
    public async Task OwnerCancelDeadline_Should_AgreeWithTheEntity_WhenTicketIsStored()
    {
        // Arrange
        Ticket ticket = QueryTicketTestData.Create(createdAtUtc: OpenedAtUtc);

        await using (ApplicationDbContext context = CreateDbContext())
        {
            context.Tickets.Add(ticket);
            await context.SaveChangesAsync();
        }

        // Act
        await using DbConnection connection = await DbConnectionFactory.OpenConnectionAsync();
        DateTime deadline = await connection.QuerySingleAsync<DateTime>(
            $"SELECT {TicketSql.OwnerCancelDeadline} FROM public.tickets t WHERE t.id = @TicketId",
            new { TicketId = ticket.Id, Ticket.OwnerCancelWindowMinutes });

        // Assert
        deadline.ShouldBe(Ticket.OwnerCancelDeadline(ticket.CreatedAtUtc));
    }

    // Um chamado em cada situação do SLA no instante informado.
    private static Ticket[] TicketsInEverySlaStatus(DateTime nowUtc) =>
    [
        QueryTicketTestData.Create(createdAtUtc: OpenedAtUtc, slaWarningAtUtc: nowUtc.AddHours(1), dueAtUtc: nowUtc.AddHours(2)),
        QueryTicketTestData.Create(status: TicketStatus.InProgress, createdAtUtc: OpenedAtUtc, slaWarningAtUtc: nowUtc.AddHours(-1), dueAtUtc: nowUtc.AddHours(1)),
        QueryTicketTestData.Create(createdAtUtc: OpenedAtUtc, slaWarningAtUtc: nowUtc.AddHours(-2), dueAtUtc: nowUtc),
        QueryTicketTestData.Create(status: TicketStatus.Resolved, createdAtUtc: OpenedAtUtc, dueAtUtc: OpenedAtUtc.AddHours(4), resolvedAtUtc: OpenedAtUtc.AddHours(3)),
        QueryTicketTestData.Create(status: TicketStatus.Closed, createdAtUtc: OpenedAtUtc, dueAtUtc: OpenedAtUtc.AddHours(4), resolvedAtUtc: OpenedAtUtc.AddHours(5)),
        QueryTicketTestData.Create(status: TicketStatus.Canceled, createdAtUtc: OpenedAtUtc, slaWarningAtUtc: nowUtc.AddHours(-2), dueAtUtc: nowUtc.AddHours(-1))
    ];

    private static TicketSlaStatus ToSlaStatus(SlaObjectiveState state) =>
        state switch
        {
            SlaObjectiveState.AtRisk => TicketSlaStatus.AtRisk,
            SlaObjectiveState.Breached => TicketSlaStatus.Breached,
            SlaObjectiveState.Met => TicketSlaStatus.Met,
            SlaObjectiveState.Missed => TicketSlaStatus.Missed,
            SlaObjectiveState.Stopped => TicketSlaStatus.NotApplicable,
            _ => TicketSlaStatus.OnTime
        };

    private async Task<List<Guid>> SelectAsync(string sql, object? parameters = null)
    {
        await using DbConnection connection = await DbConnectionFactory.OpenConnectionAsync();

        return (await connection.QueryAsync<Guid>(sql, parameters)).AsList();
    }

    public sealed class CurrentStatusRow
    {
        public Guid Id { get; init; }
        public TicketSlaStatus? Status { get; init; }
    }
}
