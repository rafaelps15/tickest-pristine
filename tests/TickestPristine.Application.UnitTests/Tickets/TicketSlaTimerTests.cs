using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class TicketSlaTimerTests
{
    private static readonly BusinessCalendar Calendar = DefaultSlaCalendar.Create();

    private static readonly TicketSlaPlan UrgentPlan =
        new(null, "Padrão", 1, SlaCalendarIds.Default, 30, 240, IsLegacy: false);

    // Segunda-feira, 26/10/2026, em São Paulo (UTC-3).
    private static DateTime SaoPaulo(int hour, int minute, int second = 0) =>
        new(2026, 10, 26, hour + 3, minute, second, DateTimeKind.Utc);

    // Chamado Urgente aberto às 08:10 com os cronômetros do ciclo 1, como a abertura pela API.
    private static (Ticket Ticket, IReadOnlyList<TicketSlaTimer> Timers) OpenTicket(
        TicketSlaPlan? plan = null,
        BusinessCalendar? calendar = null)
    {
        var requester = new User { Id = Guid.NewGuid() };

        var ticket = Ticket.Create(
            "Impressora parada",
            "A impressora do terceiro andar não liga",
            TicketPriority.Urgent,
            TicketTestData.CreateSector(),
            requester,
            assignee: null,
            plan ?? UrgentPlan,
            calendar ?? Calendar,
            requester.Id,
            SaoPaulo(8, 10));

        (_, IReadOnlyList<TicketSlaTimer> timers) = ticket.RecordOpening(requester.Id);

        return (ticket, timers);
    }

    private static TicketSlaTimer UrgentResolution() => OpenTicket().Timers.Single(t => t.Metric == SlaMetric.Resolution);

    private static TicketSlaTimer UrgentFirstResponse() => OpenTicket().Timers.Single(t => t.Metric == SlaMetric.FirstResponse);

    [Fact]
    public void RecordOpening_Should_CreateCycleOneTimersWithDeadlinesInBusinessTime()
    {
        // Act
        (Ticket ticket, IReadOnlyList<TicketSlaTimer> timers) = OpenTicket();

        // Assert
        TicketSlaTimer firstResponse = timers.Single(t => t.Metric == SlaMetric.FirstResponse);
        firstResponse.TicketId.ShouldBe(ticket.Id);
        firstResponse.Cycle.ShouldBe(1);
        firstResponse.TargetMinutes.ShouldBe(30);
        firstResponse.WarningOnUtc.ShouldBe(SaoPaulo(8, 32, 30));
        firstResponse.DueOnUtc.ShouldBe(SaoPaulo(8, 40));

        TicketSlaTimer resolution = timers.Single(t => t.Metric == SlaMetric.Resolution);
        resolution.TicketId.ShouldBe(ticket.Id);
        resolution.Cycle.ShouldBe(1);
        resolution.WarningOnUtc.ShouldBe(SaoPaulo(11, 10));
        resolution.DueOnUtc.ShouldBe(SaoPaulo(12, 10));
    }

    [Fact]
    public void Create_Should_TakeDeadlinesFromCycleOneResolution()
    {
        // Act
        (Ticket ticket, IReadOnlyList<TicketSlaTimer> timers) = OpenTicket();

        // Assert
        TicketSlaTimer resolution = timers.Single(t => t.Metric == SlaMetric.Resolution);
        ticket.DueAtUtc.ShouldBe(resolution.DueOnUtc);
        ticket.SlaWarningAtUtc.ShouldBe(resolution.WarningOnUtc);
    }

    [Fact]
    public void RecordOpening_Should_SkipFirstResponse_WhenPlanHasNoFirstResponseTarget()
    {
        // Arrange
        var legacyPlan = TicketSlaPlan.Legacy(TicketPriority.Urgent);

        // Act
        (_, IReadOnlyList<TicketSlaTimer> timers) = OpenTicket(legacyPlan, ContinuousSlaCalendar.Create());

        // Assert
        TicketSlaTimer resolution = timers.ShouldHaveSingleItem();
        resolution.Metric.ShouldBe(SlaMetric.Resolution);
        resolution.DueOnUtc.ShouldBe(SaoPaulo(12, 10));
    }

    [Fact]
    public void GetState_Should_ReturnAtRiskWithConsumed_WhenBetweenWarningAndDue()
    {
        // Arrange
        TicketSlaTimer resolution = UrgentResolution();
        DateTime nowUtc = SaoPaulo(11, 15);

        // Act
        SlaObjectiveState state = resolution.GetState(nowUtc);
        TimeSpan consumed = resolution.GetConsumed(Calendar, nowUtc);

        // Assert
        state.ShouldBe(SlaObjectiveState.AtRisk);
        consumed.ShouldBe(TimeSpan.FromMinutes(185));
    }

    [Fact]
    public void GetState_Should_ReturnBreachedWithConsumedPastTarget_WhenPastDueAndOpen()
    {
        // Arrange
        TicketSlaTimer resolution = UrgentResolution();
        DateTime nowUtc = SaoPaulo(12, 20);

        // Act
        SlaObjectiveState state = resolution.GetState(nowUtc);
        TimeSpan consumed = resolution.GetConsumed(Calendar, nowUtc);

        // Assert
        state.ShouldBe(SlaObjectiveState.Breached);
        consumed.ShouldBe(TimeSpan.FromMinutes(250));
    }

    [Fact]
    public void GetState_Should_ReturnRunning_WhenBeforeWarning()
    {
        // Act
        SlaObjectiveState state = UrgentResolution().GetState(SaoPaulo(11, 9, 59));

        // Assert
        state.ShouldBe(SlaObjectiveState.Running);
    }

    [Fact]
    public void GetState_Should_ReturnMet_WhenCompletedExactlyAtDue()
    {
        // Arrange
        TicketSlaTimer resolution = UrgentResolution();
        resolution.Complete(SaoPaulo(12, 10), SlaCompletionCause.Resolution);

        // Act
        SlaObjectiveState state = resolution.GetState(SaoPaulo(13, 0));

        // Assert
        state.ShouldBe(SlaObjectiveState.Met);
    }

    [Fact]
    public void GetState_Should_ReturnMissed_WhenCompletedAfterDue()
    {
        // Arrange
        TicketSlaTimer resolution = UrgentResolution();
        resolution.Complete(SaoPaulo(12, 30), SlaCompletionCause.Resolution);

        // Act
        SlaObjectiveState state = resolution.GetState(SaoPaulo(13, 0));

        // Assert
        state.ShouldBe(SlaObjectiveState.Missed);
    }

    [Fact]
    public void GetState_Should_ReturnStopped_WhenStoppedEvenAfterDue()
    {
        // Arrange: cancelar o chamado às 09:00 para os cronômetros em aberto.
        (Ticket ticket, IReadOnlyList<TicketSlaTimer> timers) = OpenTicket();
        TicketSlaTimer resolution = timers.Single(t => t.Metric == SlaMetric.Resolution);
        ticket.Update(ticket.Description, TicketStatus.Canceled, timers, lastMessageIsFromAgent: false, ticket.CreatedByUserId, SaoPaulo(9, 0));

        // Act
        SlaObjectiveState state = resolution.GetState(SaoPaulo(17, 0));
        TimeSpan consumed = resolution.GetConsumed(Calendar, SaoPaulo(17, 0));

        // Assert
        state.ShouldBe(SlaObjectiveState.Stopped);
        consumed.ShouldBe(TimeSpan.FromMinutes(50));
    }

    [Fact]
    public void Complete_Should_ReturnFalseAndKeepFirstCompletion_WhenCalledAgain()
    {
        // Arrange
        TicketSlaTimer resolution = UrgentResolution();
        resolution.Complete(SaoPaulo(10, 0), SlaCompletionCause.Resolution);

        // Act
        bool completedAgain = resolution.Complete(SaoPaulo(11, 0), SlaCompletionCause.Message);

        // Assert
        completedAgain.ShouldBeFalse();
        resolution.CompletedOnUtc.ShouldBe(SaoPaulo(10, 0));
        resolution.CompletionCause.ShouldBe(SlaCompletionCause.Resolution);
        resolution.StoppedOnUtc.ShouldBeNull();
    }

    [Fact]
    public void Update_Should_NotStopTimer_WhenItWasAlreadyCompleted()
    {
        // Arrange
        (Ticket ticket, IReadOnlyList<TicketSlaTimer> timers) = OpenTicket();
        TicketSlaTimer firstResponse = timers.Single(t => t.Metric == SlaMetric.FirstResponse);
        firstResponse.Complete(SaoPaulo(8, 20), SlaCompletionCause.Message);

        // Act
        ticket.Update(ticket.Description, TicketStatus.Canceled, timers, lastMessageIsFromAgent: false, ticket.CreatedByUserId, SaoPaulo(9, 0));

        // Assert
        firstResponse.CompletedOnUtc.ShouldBe(SaoPaulo(8, 20));
        firstResponse.StoppedOnUtc.ShouldBeNull();
        timers.Single(t => t.Metric == SlaMetric.Resolution).StoppedOnUtc.ShouldBe(SaoPaulo(9, 0));
    }

    [Fact]
    public void GetConsumed_Should_KeepFractionOfMinute()
    {
        // Arrange: no alerta da primeira resposta, 22 minutos e 30 segundos consumidos.
        TicketSlaTimer firstResponse = UrgentFirstResponse();
        DateTime nowUtc = SaoPaulo(8, 32, 30);

        // Act
        TimeSpan consumed = firstResponse.GetConsumed(Calendar, nowUtc);

        // Assert
        consumed.ShouldBe(new TimeSpan(0, 22, 30));
        firstResponse.GetState(nowUtc).ShouldBe(SlaObjectiveState.AtRisk);
    }

    [Fact]
    public void Reopen_Should_StartNewResolutionCycleWithFullTarget()
    {
        // Arrange
        (Ticket ticket, IReadOnlyList<TicketSlaTimer> timers) = OpenTicket();
        ticket.Update(ticket.Description, TicketStatus.Canceled, timers, lastMessageIsFromAgent: false, ticket.CreatedByUserId, SaoPaulo(9, 0));

        // Act
        (_, TicketSlaTimer cycle) = ticket.Reopen(timers, Calendar, lastMessageIsFromAgent: false, ticket.CreatedByUserId, SaoPaulo(14, 0));

        // Assert
        cycle.TicketId.ShouldBe(ticket.Id);
        cycle.Metric.ShouldBe(SlaMetric.Resolution);
        cycle.Cycle.ShouldBe(2);
        cycle.TargetMinutes.ShouldBe(240);
        cycle.StartedAtUtc.ShouldBe(SaoPaulo(14, 0));
        cycle.DueOnUtc.ShouldBe(SaoPaulo(18, 0));
    }

    [Fact]
    public void Update_Should_CompleteHighestResolutionCycle_WhenResolving()
    {
        // Arrange: ciclo 1 parado no cancelamento e ciclo 2 aberto na reabertura.
        (Ticket ticket, IReadOnlyList<TicketSlaTimer> openingTimers) = OpenTicket();
        ticket.Update(ticket.Description, TicketStatus.Canceled, openingTimers, lastMessageIsFromAgent: false, ticket.CreatedByUserId, SaoPaulo(9, 0));
        (_, TicketSlaTimer cycle2) = ticket.Reopen(openingTimers, Calendar, lastMessageIsFromAgent: false, ticket.CreatedByUserId, SaoPaulo(14, 0));
        List<TicketSlaTimer> timers = [.. openingTimers, cycle2];
        ticket.Update(ticket.Description, TicketStatus.InProgress, timers, lastMessageIsFromAgent: false, ticket.CreatedByUserId, SaoPaulo(14, 30));

        // Act
        ticket.Update(ticket.Description, TicketStatus.Resolved, timers, lastMessageIsFromAgent: false, ticket.CreatedByUserId, SaoPaulo(15, 0));

        // Assert
        cycle2.CompletedOnUtc.ShouldBe(SaoPaulo(15, 0));
        cycle2.CompletionCause.ShouldBe(SlaCompletionCause.Resolution);
        openingTimers.Single(t => t.Metric == SlaMetric.Resolution).CompletedOnUtc.ShouldBeNull();
    }

    [Fact]
    public void RecordOpening_Should_QueueEachTimerAtItsDeadline()
    {
        // Act
        (_, IReadOnlyList<TicketSlaTimer> timers) = OpenTicket();

        // Assert
        timers.ShouldAllBe(t => t.NextActionOnUtc == t.DueOnUtc && t.BreachedOnUtc == null);
    }

    [Fact]
    public void Complete_Should_DequeueTimer_WhenCompletedUntilDeadline()
    {
        // Arrange
        TicketSlaTimer resolution = UrgentResolution();

        // Act
        bool completed = resolution.Complete(SaoPaulo(12, 10), SlaCompletionCause.Resolution);

        // Assert
        completed.ShouldBeTrue();
        resolution.NextActionOnUtc.ShouldBeNull();
    }

    [Fact]
    public void Complete_Should_KeepTimerQueued_WhenCompletedAfterDeadline()
    {
        // Arrange
        TicketSlaTimer resolution = UrgentResolution();

        // Act
        resolution.Complete(SaoPaulo(12, 30), SlaCompletionCause.Resolution);

        // Assert
        resolution.NextActionOnUtc.ShouldBe(resolution.DueOnUtc);
        resolution.BreachedOnUtc.ShouldBeNull();
    }

    [Fact]
    public void Update_Should_StopAndDequeueOpenTimers_WhenCanceling()
    {
        // Arrange
        (Ticket ticket, IReadOnlyList<TicketSlaTimer> timers) = OpenTicket();

        // Act
        ticket.Update(ticket.Description, TicketStatus.Canceled, timers, lastMessageIsFromAgent: false, ticket.CreatedByUserId, SaoPaulo(13, 0));

        // Assert
        timers.ShouldAllBe(t => t.StoppedOnUtc == SaoPaulo(13, 0) && t.NextActionOnUtc == null && t.BreachedOnUtc == null);
    }

    [Fact]
    public void ProcessDue_Should_RecordBreachAtDeadlineAndRaiseEvent_WhenTimerIsOpen()
    {
        // Arrange
        TicketSlaTimer firstResponse = UrgentFirstResponse();

        // Act
        bool breached = firstResponse.ProcessDue();

        // Assert
        breached.ShouldBeTrue();
        firstResponse.BreachedOnUtc.ShouldBe(SaoPaulo(8, 40));
        firstResponse.NextActionOnUtc.ShouldBeNull();
        firstResponse.DomainEvents.ShouldHaveSingleItem().ShouldBe(
            new TicketSlaBreachedDomainEvent(firstResponse.TicketId, SlaMetric.FirstResponse, 1, SaoPaulo(8, 40)));
    }

    [Fact]
    public void ProcessDue_Should_RecordBreach_WhenTimerWasCompletedAfterDeadline()
    {
        // Arrange
        TicketSlaTimer firstResponse = UrgentFirstResponse();
        firstResponse.Complete(SaoPaulo(8, 45), SlaCompletionCause.Message);

        // Act
        bool breached = firstResponse.ProcessDue();

        // Assert
        breached.ShouldBeTrue();
        firstResponse.BreachedOnUtc.ShouldBe(SaoPaulo(8, 40));
        firstResponse.CompletedOnUtc.ShouldBe(SaoPaulo(8, 45));
        firstResponse.DomainEvents.ShouldHaveSingleItem();
    }

    [Fact]
    public void ProcessDue_Should_OnlyDequeue_WhenTimerWasCompletedOnTimeOrStopped()
    {
        // Arrange: concluído no prazo, mas ainda na fila (lido antes da conclusão), e parado no cancelamento.
        TicketSlaTimer completedOnTime = UrgentFirstResponse();
        completedOnTime.Complete(SaoPaulo(8, 30), SlaCompletionCause.Message);
        completedOnTime.With(nameof(TicketSlaTimer.NextActionOnUtc), completedOnTime.DueOnUtc);

        (Ticket ticket, IReadOnlyList<TicketSlaTimer> timers) = OpenTicket();
        ticket.Update(ticket.Description, TicketStatus.Canceled, timers, lastMessageIsFromAgent: false, ticket.CreatedByUserId, SaoPaulo(9, 0));
        TicketSlaTimer stopped = timers.Single(t => t.Metric == SlaMetric.Resolution);

        // Act
        bool completedBreached = completedOnTime.ProcessDue();
        bool stoppedBreached = stopped.ProcessDue();

        // Assert
        completedBreached.ShouldBeFalse();
        stoppedBreached.ShouldBeFalse();
        new[] { completedOnTime, stopped }.ShouldAllBe(t => t.BreachedOnUtc == null && t.NextActionOnUtc == null && !t.DomainEvents.Any());
    }

    [Fact]
    public void Reopen_Should_QueueNewCycleAtItsDeadline()
    {
        // Arrange
        (Ticket ticket, IReadOnlyList<TicketSlaTimer> timers) = OpenTicket();
        ticket.Update(ticket.Description, TicketStatus.Canceled, timers, lastMessageIsFromAgent: false, ticket.CreatedByUserId, SaoPaulo(9, 0));

        // Act: reaberto às 14:00; 240 minutos úteis terminam às 18:00, na borda da faixa.
        (_, TicketSlaTimer cycle) = ticket.Reopen(timers, Calendar, lastMessageIsFromAgent: false, ticket.CreatedByUserId, SaoPaulo(14, 0));

        // Assert
        cycle.NextActionOnUtc.ShouldBe(SaoPaulo(18, 0));
    }

    [Fact]
    public void RecordOpening_Should_ReturnTheTimersCreatedByCreate_WhenCalledAgain()
    {
        // Arrange
        (Ticket ticket, IReadOnlyList<TicketSlaTimer> firstCall) = OpenTicket();

        // Act
        (_, IReadOnlyList<TicketSlaTimer> secondCall) = ticket.RecordOpening(ticket.CreatedByUserId);

        // Assert
        secondCall.ShouldBe(firstCall);
        secondCall.Count.ShouldBe(2);
    }

    [Fact]
    public void Update_Should_IgnoreTimersOfAnotherTicket_WhenResolvingOrCanceling()
    {
        // Arrange
        (Ticket resolved, IReadOnlyList<TicketSlaTimer> resolvedTimers) = OpenTicket();
        (Ticket canceled, IReadOnlyList<TicketSlaTimer> canceledTimers) = OpenTicket();
        (_, IReadOnlyList<TicketSlaTimer> otherTimers) = OpenTicket();
        resolved.Update(resolved.Description, TicketStatus.InProgress, [], lastMessageIsFromAgent: false, resolved.CreatedByUserId, SaoPaulo(8, 30));

        // Act
        resolved.Update(resolved.Description, TicketStatus.Resolved, [.. resolvedTimers, .. otherTimers], lastMessageIsFromAgent: false, resolved.CreatedByUserId, SaoPaulo(9, 0));
        canceled.Update(canceled.Description, TicketStatus.Canceled, [.. canceledTimers, .. otherTimers], lastMessageIsFromAgent: false, canceled.CreatedByUserId, SaoPaulo(9, 0));

        // Assert
        resolvedTimers.ShouldAllBe(t => t.CompletedOnUtc == SaoPaulo(9, 0));
        canceledTimers.ShouldAllBe(t => t.StoppedOnUtc == SaoPaulo(9, 0));
        otherTimers.ShouldAllBe(t => t.IsOpen && t.NextActionOnUtc == t.DueOnUtc);
    }

    [Fact]
    public void Reopen_Should_CountOnlyCyclesOfTheSameTicket()
    {
        // Arrange: o outro chamado já está no ciclo 2.
        (Ticket ticket, IReadOnlyList<TicketSlaTimer> timers) = OpenTicket();
        (Ticket other, IReadOnlyList<TicketSlaTimer> otherTimers) = OpenTicket();
        other.Update(other.Description, TicketStatus.Canceled, otherTimers, lastMessageIsFromAgent: false, other.CreatedByUserId, SaoPaulo(9, 0));
        (_, TicketSlaTimer otherCycle2) = other.Reopen(otherTimers, Calendar, lastMessageIsFromAgent: false, other.CreatedByUserId, SaoPaulo(10, 0));
        ticket.Update(ticket.Description, TicketStatus.Canceled, timers, lastMessageIsFromAgent: false, ticket.CreatedByUserId, SaoPaulo(9, 0));

        // Act
        (_, TicketSlaTimer cycle) = ticket.Reopen([.. timers, .. otherTimers, otherCycle2], Calendar, lastMessageIsFromAgent: false, ticket.CreatedByUserId, SaoPaulo(14, 0));

        // Assert
        cycle.Cycle.ShouldBe(2);
    }

    [Fact]
    public void Delete_Should_IgnoreTimersOfAnotherTicket()
    {
        // Arrange
        (Ticket ticket, IReadOnlyList<TicketSlaTimer> timers) = OpenTicket();
        (_, IReadOnlyList<TicketSlaTimer> otherTimers) = OpenTicket();

        // Act
        ticket.Delete([.. timers, .. otherTimers], ticket.CreatedByUserId, SaoPaulo(9, 0));

        // Assert
        timers.ShouldAllBe(t => t.NextActionOnUtc == null);
        otherTimers.ShouldAllBe(t => t.NextActionOnUtc == t.DueOnUtc);
    }

    [Fact]
    public void Delete_Should_DequeueTimersWithoutBreachOrStop()
    {
        // Arrange
        (Ticket ticket, IReadOnlyList<TicketSlaTimer> timers) = OpenTicket();

        // Act
        ticket.Delete(timers, ticket.CreatedByUserId, SaoPaulo(9, 0));

        // Assert
        timers.ShouldAllBe(t =>
            t.NextActionOnUtc == null && t.BreachedOnUtc == null && t.StoppedOnUtc == null && !t.DomainEvents.Any());
    }
}
