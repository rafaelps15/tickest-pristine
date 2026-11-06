using TickestPristine.Domain.SlaCalendars;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.TicketHistories;

namespace TickestPristine.Domain.Tickets;

/// <summary>
/// Transições do chamado com efeito no SLA, entre o <see cref="Ticket"/> e os <see cref="TicketSlaTimer"/> dele, que são
/// agregados independentes; não lê nem grava nada, e o handler persiste o resultado num único save. Cada operação
/// recebe os cronômetros do chamado já carregados: os de outro chamado na coleção são ignorados e nunca mudam. Os
/// cronômetros a mudar são escolhidos e todo cálculo que pode lançar exceção roda antes da primeira mudança; o chamado
/// muda pelo próprio método, que confere as regras dele antes de mudar; os cronômetros só mudam depois que o chamado
/// aceitou a transição. Em falha, nem o chamado nem os cronômetros mudam.
/// </summary>
public sealed class TicketSlaCycleService
{
    /// <summary>
    /// Cronômetros do ciclo 1 do chamado recém-aberto, para o handler adicionar junto com ele: o de resolução com o
    /// prazo e o alerta já gravados no chamado e o de primeira resposta, quando o plano tem essa meta, no tempo útil do
    /// calendário. Não muda o chamado. Lança <see cref="InvalidOperationException"/> quando o calendário não tem tempo
    /// útil suficiente.
    /// </summary>
    public IReadOnlyList<TicketSlaTimer> Start(Ticket ticket, BusinessCalendar calendar)
    {
        TicketSlaPlan plan = ticket.SlaPlan;

        var resolution = TicketSlaTimer.Create(
            ticket.Id,
            SlaMetric.Resolution,
            1,
            plan.ResolutionMinutes,
            plan.CalendarId,
            ticket.CreatedAtUtc,
            new SlaDeadlines(ticket.SlaWarningAtUtc, ticket.DueAtUtc));

        if (plan.FirstResponseMinutes is not { } firstResponseMinutes)
        {
            return [resolution];
        }

        var firstResponse = TicketSlaTimer.Create(
            ticket.Id,
            SlaMetric.FirstResponse,
            1,
            firstResponseMinutes,
            plan.CalendarId,
            ticket.CreatedAtUtc,
            TicketSlaPlan.CalculateDeadlines(firstResponseMinutes, calendar, ticket.CreatedAtUtc));

        return [firstResponse, resolution];
    }

    /// <summary>
    /// Resolve o chamado e conclui, com a causa "resolução", o ciclo corrente de resolução e a primeira resposta, cada um
    /// só se ainda estiver aberto. Falha como o <see cref="Ticket"/>, sem mudar nada.
    /// </summary>
    public Result<TicketHistoryEntry> Resolve(
        Ticket ticket,
        IReadOnlyCollection<TicketSlaTimer> slaTimers,
        Guid resolvedByUserId,
        DateTime utcNow)
    {
        TicketSlaTimer? currentResolution = CurrentResolutionCycle(ticket, slaTimers);

        // A primeira resposta só existe no ciclo 1.
        TicketSlaTimer? firstResponse = slaTimers.FirstOrDefault(t => t.BelongsTo(ticket) && t.Metric == SlaMetric.FirstResponse);

        Result<TicketHistoryEntry> resolving = ticket.Resolve(resolvedByUserId, utcNow);

        if (resolving.IsFailure)
        {
            return resolving;
        }

        if (currentResolution is { IsOpen: true })
        {
            currentResolution.Complete(utcNow, SlaCompletionCause.Resolution);
        }

        // Resolver sem ter respondido também conclui a primeira resposta, mas com a causa "resolução": a média de primeira
        // resposta conta só as concluídas por mensagem.
        if (firstResponse is { IsOpen: true })
        {
            firstResponse.Complete(utcNow, SlaCompletionCause.Resolution);
        }

        return resolving;
    }

    /// <summary>
    /// Cancela o chamado e para os cronômetros dele que estão abertos; os concluídos não mudam. Falha como o
    /// <see cref="Ticket"/>, sem mudar nada.
    /// </summary>
    public Result<TicketHistoryEntry> Cancel(
        Ticket ticket,
        IReadOnlyCollection<TicketSlaTimer> slaTimers,
        bool canCancelAny,
        bool isOwnerWhoCanEdit,
        Guid canceledByUserId,
        DateTime utcNow)
    {
        TicketSlaTimer[] openTimers = [.. slaTimers.Where(t => t.BelongsTo(ticket) && t.IsOpen)];

        Result<TicketHistoryEntry> cancellation = ticket.Cancel(canCancelAny, isOwnerWhoCanEdit, canceledByUserId, utcNow);

        if (cancellation.IsFailure)
        {
            return cancellation;
        }

        foreach (TicketSlaTimer timer in openTimers)
        {
            timer.Stop(utcNow);
        }

        return cancellation;
    }

    /// <summary>
    /// Reabre o chamado com um novo ciclo de resolução, o seguinte ao ciclo corrente (ou o 1, sem ciclos anteriores),
    /// com a meta integral do plano; os ciclos anteriores e a primeira resposta não mudam. Falha como o
    /// <see cref="Ticket"/>, inclusive com <see cref="TicketErrors.ResolutionCycleAlreadyOpen"/> quando ainda há um ciclo
    /// de resolução aberto, e lança <see cref="InvalidOperationException"/> quando o calendário não tem tempo útil
    /// suficiente, sempre sem mudar nada.
    /// </summary>
    public Result<TicketReopening> Reopen(
        Ticket ticket,
        IReadOnlyCollection<TicketSlaTimer> slaTimers,
        BusinessCalendar calendar,
        bool lastMessageIsFromAgent,
        Guid reopenedByUserId,
        DateTime utcNow)
    {
        bool hasOpenResolutionCycle = slaTimers.Any(t => t.BelongsTo(ticket) && t.Metric == SlaMetric.Resolution && t.IsOpen);

        // As recusas do chamado vêm antes do cálculo do prazo, que pode lançar exceção.
        Result canReopen = ticket.EnsureCanReopen(hasOpenResolutionCycle);

        if (canReopen.IsFailure)
        {
            return Result.Failure<TicketReopening>(canReopen.Error);
        }

        int nextCycle = CurrentResolutionCycle(ticket, slaTimers) is { } current ? current.Cycle + 1 : 1;

        // O cronômetro novo ainda não está em lugar algum: se o cálculo lançar exceção, nada mudou.
        TicketSlaPlan plan = ticket.SlaPlan;

        var resolutionCycle = TicketSlaTimer.Create(
            ticket.Id,
            SlaMetric.Resolution,
            nextCycle,
            plan.ResolutionMinutes,
            plan.CalendarId,
            utcNow,
            TicketSlaPlan.CalculateDeadlines(plan.ResolutionMinutes, calendar, utcNow));

        Result<TicketHistoryEntry> reopening = ticket.Reopen(hasOpenResolutionCycle, lastMessageIsFromAgent, reopenedByUserId, utcNow);

        if (reopening.IsFailure)
        {
            return Result.Failure<TicketReopening>(reopening.Error);
        }

        return new TicketReopening(reopening.Value, resolutionCycle);
    }

    /// <summary>
    /// Exclui o chamado logicamente e tira da fila do monitoramento os cronômetros dele que ainda estão nela, sem
    /// violação, parada nem evento. Falha como o <see cref="Ticket"/>, sem mudar nada.
    /// </summary>
    public Result<TicketHistoryEntry> Delete(
        Ticket ticket,
        IReadOnlyCollection<TicketSlaTimer> slaTimers,
        Guid deletedByUserId,
        DateTime utcNow)
    {
        TicketSlaTimer[] queuedTimers = [.. slaTimers.Where(t => t.BelongsTo(ticket) && t.IsQueued)];

        Result<TicketHistoryEntry> deletion = ticket.Delete(deletedByUserId, utcNow);

        if (deletion.IsFailure)
        {
            return deletion;
        }

        foreach (TicketSlaTimer timer in queuedTimers)
        {
            timer.Dequeue();
        }

        return deletion;
    }

    // O ciclo corrente de resolução é o de maior número, aberto ou não; nulo quando o chamado não tem nenhum.
    private static TicketSlaTimer? CurrentResolutionCycle(Ticket ticket, IEnumerable<TicketSlaTimer> slaTimers) =>
        slaTimers
            .Where(t => t.BelongsTo(ticket) && t.Metric == SlaMetric.Resolution)
            .OrderByDescending(t => t.Cycle)
            .FirstOrDefault();
}
