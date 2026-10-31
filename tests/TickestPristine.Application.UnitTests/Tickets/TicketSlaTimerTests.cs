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

    // Quem atende o chamado nos testes da primeira resposta; não é o solicitante.
    private static readonly Guid AgentId = Guid.NewGuid();

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

        IReadOnlyList<TicketSlaTimer> timers = new TicketSlaCycleService().Start(ticket, calendar ?? Calendar);

        return (ticket, timers);
    }

    private static TicketSlaTimer UrgentResolution() => OpenTicket().Timers.Single(t => t.Metric == SlaMetric.Resolution);

    private static TicketSlaTimer UrgentFirstResponse() => OpenTicket().Timers.Single(t => t.Metric == SlaMetric.FirstResponse);

    private static (Ticket Ticket, TicketSlaTimer FirstResponse) OpenFirstResponse()
    {
        (Ticket ticket, IReadOnlyList<TicketSlaTimer> timers) = OpenTicket();

        return (ticket, timers.Single(t => t.Metric == SlaMetric.FirstResponse));
    }

    // Em aberto, na fila até o prazo e sem eventos, como na abertura.
    private static void ShouldBeUnchangedOpenTimer(TicketSlaTimer timer)
    {
        timer.CompletedOnUtc.ShouldBeNull();
        timer.CompletionCause.ShouldBeNull();
        timer.StoppedOnUtc.ShouldBeNull();
        timer.NextActionOnUtc.ShouldBe(timer.DueOnUtc);
        timer.DomainEvents.ShouldBeEmpty();
    }

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
        (Ticket ticket, TicketSlaTimer firstResponse) = OpenFirstResponse();
        firstResponse.CompleteFirstResponse(ticket, AgentId, authorIsAgent: true, SaoPaulo(8, 40));

        // Act
        SlaObjectiveState state = firstResponse.GetState(SaoPaulo(13, 0));

        // Assert
        state.ShouldBe(SlaObjectiveState.Met);
    }

    [Fact]
    public void GetState_Should_ReturnMissed_WhenCompletedAfterDue()
    {
        // Arrange
        (Ticket ticket, TicketSlaTimer firstResponse) = OpenFirstResponse();
        firstResponse.CompleteFirstResponse(ticket, AgentId, authorIsAgent: true, SaoPaulo(9, 0));

        // Act
        SlaObjectiveState state = firstResponse.GetState(SaoPaulo(13, 0));

        // Assert
        state.ShouldBe(SlaObjectiveState.Missed);
    }

    [Fact]
    public void GetState_Should_ReturnStopped_WhenStoppedEvenAfterDue()
    {
        // Arrange: cancelar o chamado às 09:00 para os cronômetros em aberto.
        (Ticket ticket, IReadOnlyList<TicketSlaTimer> timers) = OpenTicket();
        TicketSlaTimer resolution = timers.Single(t => t.Metric == SlaMetric.Resolution);
        new TicketSlaCycleService().Cancel(ticket, timers, canCancelAny: true, isOwnerWhoCanEdit: false, ticket.CreatedByUserId, SaoPaulo(9, 0));

        // Act
        SlaObjectiveState state = resolution.GetState(SaoPaulo(17, 0));
        TimeSpan consumed = resolution.GetConsumed(Calendar, SaoPaulo(17, 0));

        // Assert
        state.ShouldBe(SlaObjectiveState.Stopped);
        consumed.ShouldBe(TimeSpan.FromMinutes(50));
    }

    [Fact]
    public void CompleteFirstResponse_Should_ReturnFalseAndKeepFirstCompletion_WhenCalledAgain()
    {
        // Arrange
        (Ticket ticket, TicketSlaTimer firstResponse) = OpenFirstResponse();
        firstResponse.CompleteFirstResponse(ticket, AgentId, authorIsAgent: true, SaoPaulo(8, 20));

        // Act
        bool completedAgain = firstResponse.CompleteFirstResponse(ticket, Guid.NewGuid(), authorIsAgent: true, SaoPaulo(8, 30));

        // Assert
        completedAgain.ShouldBeFalse();
        firstResponse.CompletedOnUtc.ShouldBe(SaoPaulo(8, 20));
        firstResponse.CompletionCause.ShouldBe(SlaCompletionCause.Message);
        firstResponse.StoppedOnUtc.ShouldBeNull();
    }

    [Fact]
    public void CompleteFirstResponse_Should_CompleteWithMessageCause_WhenAgentAnswers()
    {
        // Arrange
        (Ticket ticket, TicketSlaTimer firstResponse) = OpenFirstResponse();

        // Act
        bool completed = firstResponse.CompleteFirstResponse(ticket, AgentId, authorIsAgent: true, SaoPaulo(8, 20));

        // Assert
        completed.ShouldBeTrue();
        firstResponse.CompletedOnUtc.ShouldBe(SaoPaulo(8, 20));
        firstResponse.CompletionCause.ShouldBe(SlaCompletionCause.Message);
    }

    [Fact]
    public void CompleteFirstResponse_Should_ReturnFalseAndChangeNothing_WhenAuthorIsRequesterOrNotAgent()
    {
        // Arrange
        (Ticket ticket, TicketSlaTimer firstResponse) = OpenFirstResponse();

        // Act
        bool byRequester = firstResponse.CompleteFirstResponse(ticket, ticket.CreatedByUserId, authorIsAgent: true, SaoPaulo(8, 20));
        bool byNonAgent = firstResponse.CompleteFirstResponse(ticket, AgentId, authorIsAgent: false, SaoPaulo(8, 25));

        // Assert
        byRequester.ShouldBeFalse();
        byNonAgent.ShouldBeFalse();
        ShouldBeUnchangedOpenTimer(firstResponse);
    }

    [Fact]
    public void CompleteFirstResponse_Should_ReturnFalseAndChangeNothing_WhenTimerIsNotOpenFirstResponseOfTicket()
    {
        // Arrange: o cronômetro de resolução do chamado, a primeira resposta de outro chamado e uma já concluída.
        (Ticket ticket, IReadOnlyList<TicketSlaTimer> timers) = OpenTicket();
        TicketSlaTimer resolution = timers.Single(t => t.Metric == SlaMetric.Resolution);
        TicketSlaTimer otherTicketFirstResponse = UrgentFirstResponse();
        (Ticket answeredTicket, TicketSlaTimer completed) = OpenFirstResponse();
        completed.CompleteFirstResponse(answeredTicket, AgentId, authorIsAgent: true, SaoPaulo(8, 20));

        // Act
        bool resolutionCompleted = resolution.CompleteFirstResponse(ticket, AgentId, authorIsAgent: true, SaoPaulo(8, 30));
        bool otherTicketCompleted = otherTicketFirstResponse.CompleteFirstResponse(ticket, AgentId, authorIsAgent: true, SaoPaulo(8, 30));
        bool completedAgain = completed.CompleteFirstResponse(answeredTicket, AgentId, authorIsAgent: true, SaoPaulo(8, 30));

        // Assert
        resolutionCompleted.ShouldBeFalse();
        otherTicketCompleted.ShouldBeFalse();
        completedAgain.ShouldBeFalse();
        ShouldBeUnchangedOpenTimer(resolution);
        ShouldBeUnchangedOpenTimer(otherTicketFirstResponse);
        completed.CompletedOnUtc.ShouldBe(SaoPaulo(8, 20));
        completed.NextActionOnUtc.ShouldBeNull();
        completed.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Cancel_Should_NotStopTimer_WhenItWasAlreadyCompleted()
    {
        // Arrange
        (Ticket ticket, IReadOnlyList<TicketSlaTimer> timers) = OpenTicket();
        TicketSlaTimer firstResponse = timers.Single(t => t.Metric == SlaMetric.FirstResponse);
        firstResponse.CompleteFirstResponse(ticket, AgentId, authorIsAgent: true, SaoPaulo(8, 20));

        // Act
        new TicketSlaCycleService().Cancel(ticket, timers, canCancelAny: true, isOwnerWhoCanEdit: false, ticket.CreatedByUserId, SaoPaulo(9, 0));

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
        new TicketSlaCycleService().Cancel(ticket, timers, canCancelAny: true, isOwnerWhoCanEdit: false, ticket.CreatedByUserId, SaoPaulo(9, 0));

        // Act
        TicketSlaTimer cycle = new TicketSlaCycleService()
            .Reopen(ticket, timers, Calendar, lastMessageIsFromAgent: false, ticket.CreatedByUserId, SaoPaulo(14, 0)).Value.ResolutionCycle;

        // Assert
        cycle.TicketId.ShouldBe(ticket.Id);
        cycle.Metric.ShouldBe(SlaMetric.Resolution);
        cycle.Cycle.ShouldBe(2);
        cycle.TargetMinutes.ShouldBe(240);
        cycle.StartedAtUtc.ShouldBe(SaoPaulo(14, 0));
        cycle.DueOnUtc.ShouldBe(SaoPaulo(18, 0));
    }

    [Fact]
    public void Resolve_Should_CompleteHighestResolutionCycle_WhenResolving()
    {
        // Arrange: ciclo 1 parado no cancelamento e ciclo 2 aberto na reabertura.
        var slaCycle = new TicketSlaCycleService();
        (Ticket ticket, IReadOnlyList<TicketSlaTimer> openingTimers) = OpenTicket();
        slaCycle.Cancel(ticket, openingTimers, canCancelAny: true, isOwnerWhoCanEdit: false, ticket.CreatedByUserId, SaoPaulo(9, 0));
        TicketSlaTimer cycle2 = slaCycle
            .Reopen(ticket, openingTimers, Calendar, lastMessageIsFromAgent: false, ticket.CreatedByUserId, SaoPaulo(14, 0)).Value.ResolutionCycle;
        List<TicketSlaTimer> timers = [.. openingTimers, cycle2];
        ticket.StartProgress(lastMessageIsFromAgent: false, ticket.CreatedByUserId, SaoPaulo(14, 30));

        // Act
        slaCycle.Resolve(ticket, timers, ticket.CreatedByUserId, SaoPaulo(15, 0));

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
    public void CompleteFirstResponse_Should_DequeueTimer_WhenCompletedUntilDeadline()
    {
        // Arrange
        (Ticket ticket, TicketSlaTimer firstResponse) = OpenFirstResponse();

        // Act
        bool completed = firstResponse.CompleteFirstResponse(ticket, AgentId, authorIsAgent: true, SaoPaulo(8, 40));

        // Assert
        completed.ShouldBeTrue();
        firstResponse.NextActionOnUtc.ShouldBeNull();
    }

    [Fact]
    public void CompleteFirstResponse_Should_KeepTimerQueued_WhenCompletedAfterDeadline()
    {
        // Arrange
        (Ticket ticket, TicketSlaTimer firstResponse) = OpenFirstResponse();

        // Act
        firstResponse.CompleteFirstResponse(ticket, AgentId, authorIsAgent: true, SaoPaulo(9, 0));

        // Assert
        firstResponse.NextActionOnUtc.ShouldBe(firstResponse.DueOnUtc);
        firstResponse.BreachedOnUtc.ShouldBeNull();
    }

    [Fact]
    public void Cancel_Should_StopAndDequeueOpenTimers_WhenCanceling()
    {
        // Arrange
        (Ticket ticket, IReadOnlyList<TicketSlaTimer> timers) = OpenTicket();

        // Act
        new TicketSlaCycleService().Cancel(ticket, timers, canCancelAny: true, isOwnerWhoCanEdit: false, ticket.CreatedByUserId, SaoPaulo(13, 0));

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
        (Ticket ticket, TicketSlaTimer firstResponse) = OpenFirstResponse();
        firstResponse.CompleteFirstResponse(ticket, AgentId, authorIsAgent: true, SaoPaulo(8, 45));

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
        (Ticket answeredTicket, TicketSlaTimer completedOnTime) = OpenFirstResponse();
        completedOnTime.CompleteFirstResponse(answeredTicket, AgentId, authorIsAgent: true, SaoPaulo(8, 30));
        completedOnTime.With(nameof(TicketSlaTimer.NextActionOnUtc), completedOnTime.DueOnUtc);

        (Ticket ticket, IReadOnlyList<TicketSlaTimer> timers) = OpenTicket();
        new TicketSlaCycleService().Cancel(ticket, timers, canCancelAny: true, isOwnerWhoCanEdit: false, ticket.CreatedByUserId, SaoPaulo(9, 0));
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
        new TicketSlaCycleService().Cancel(ticket, timers, canCancelAny: true, isOwnerWhoCanEdit: false, ticket.CreatedByUserId, SaoPaulo(9, 0));

        // Act: reaberto às 14:00; 240 minutos úteis terminam às 18:00, na borda da faixa.
        TicketSlaTimer cycle = new TicketSlaCycleService()
            .Reopen(ticket, timers, Calendar, lastMessageIsFromAgent: false, ticket.CreatedByUserId, SaoPaulo(14, 0)).Value.ResolutionCycle;

        // Assert
        cycle.NextActionOnUtc.ShouldBe(SaoPaulo(18, 0));
    }

    [Fact]
    public void ResolveAndCancel_Should_IgnoreTimersOfAnotherTicket()
    {
        // Arrange
        (Ticket resolved, IReadOnlyList<TicketSlaTimer> resolvedTimers) = OpenTicket();
        (Ticket canceled, IReadOnlyList<TicketSlaTimer> canceledTimers) = OpenTicket();
        (_, IReadOnlyList<TicketSlaTimer> otherTimers) = OpenTicket();
        var slaCycle = new TicketSlaCycleService();
        resolved.StartProgress(lastMessageIsFromAgent: false, resolved.CreatedByUserId, SaoPaulo(8, 30));

        // Act
        slaCycle.Resolve(resolved, [.. resolvedTimers, .. otherTimers], resolved.CreatedByUserId, SaoPaulo(9, 0));
        slaCycle.Cancel(canceled, [.. canceledTimers, .. otherTimers], canCancelAny: true, isOwnerWhoCanEdit: false, canceled.CreatedByUserId, SaoPaulo(9, 0));

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
        var slaCycle = new TicketSlaCycleService();
        slaCycle.Cancel(other, otherTimers, canCancelAny: true, isOwnerWhoCanEdit: false, other.CreatedByUserId, SaoPaulo(9, 0));
        TicketSlaTimer otherCycle2 = slaCycle
            .Reopen(other, otherTimers, Calendar, lastMessageIsFromAgent: false, other.CreatedByUserId, SaoPaulo(10, 0)).Value.ResolutionCycle;
        slaCycle.Cancel(ticket, timers, canCancelAny: true, isOwnerWhoCanEdit: false, ticket.CreatedByUserId, SaoPaulo(9, 0));

        // Act
        TicketSlaTimer cycle = slaCycle
            .Reopen(ticket, [.. timers, .. otherTimers, otherCycle2], Calendar, lastMessageIsFromAgent: false, ticket.CreatedByUserId, SaoPaulo(14, 0)).Value.ResolutionCycle;

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
        new TicketSlaCycleService().Delete(ticket, [.. timers, .. otherTimers], ticket.CreatedByUserId, SaoPaulo(9, 0));

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
        new TicketSlaCycleService().Delete(ticket, timers, ticket.CreatedByUserId, SaoPaulo(9, 0));

        // Assert
        timers.ShouldAllBe(t =>
            t.NextActionOnUtc == null && t.BreachedOnUtc == null && t.StoppedOnUtc == null && !t.DomainEvents.Any());
    }
}
