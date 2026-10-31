using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.SlaCalendars;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed class Ticket : Entity, IAuditable
{
    public const int OwnerCancelWindowMinutes = 15;

    private readonly List<TicketSlaTimer> _openingSlaTimers = [];

    private Ticket()
    {
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; }

    public string Description { get; private set; }

    public TicketPriority Priority { get; private set; }

    public TicketStatus Status { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public Guid? AssignedToUserId { get; private set; }

    public Guid SectorId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime LastActivityAtUtc { get; private set; }

    public TicketWaitingOn? WaitingOn { get; private set; }

    public DateTime DueAtUtc { get; private set; }

    public DateTime SlaWarningAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    public TicketSlaPlan SlaPlan { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    public bool IsActive => Status is TicketStatus.Open or TicketStatus.InProgress;

    /// <summary>
    /// Abre o chamado com o plano de SLA e os cronômetros do ciclo 1 (primeira resposta, quando o plano tem essa meta, e
    /// resolução), todos no tempo útil do calendário do plano; o prazo e o alerta do chamado são os do cronômetro de
    /// resolução. Sem mensagens, o chamado aguarda quem atende.
    /// </summary>
    public static Ticket Create(
        string title,
        string description,
        TicketPriority priority,
        Sector sector,
        User requester,
        User? assignee,
        TicketSlaPlan slaPlan,
        BusinessCalendar calendar,
        Guid openedByUserId,
        DateTime utcNow)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            Priority = priority,
            Status = TicketStatus.Open,
            CreatedByUserId = requester.Id,
            AssignedToUserId = assignee?.Id,
            SectorId = sector.Id,
            CreatedAtUtc = utcNow,
            LastActivityAtUtc = utcNow,
            WaitingOn = TicketWaitingOn.Agent,
            SlaPlan = slaPlan
        };

        if (slaPlan.FirstResponseMinutes is { } firstResponseMinutes)
        {
            ticket._openingSlaTimers.Add(TicketSlaTimer.Create(ticket.Id, SlaMetric.FirstResponse, 1, firstResponseMinutes, slaPlan.CalendarId, calendar, utcNow));
        }

        var resolutionTimer = TicketSlaTimer.Create(ticket.Id, SlaMetric.Resolution, 1, slaPlan.ResolutionMinutes, slaPlan.CalendarId, calendar, utcNow);

        ticket._openingSlaTimers.Add(resolutionTimer);
        ticket.DueAtUtc = resolutionTimer.DueOnUtc;
        ticket.SlaWarningAtUtc = resolutionTimer.WarningOnUtc;

        ticket.Raise(new TicketCreatedDomainEvent(ticket.Id, openedByUserId));

        return ticket;
    }

    /// <summary>
    /// Entrada de abertura do histórico, com a data de abertura, e os cronômetros do ciclo 1 criados pelo
    /// <see cref="Create"/>; o handler adiciona os dois junto com o chamado. Um chamado lido do banco não tem
    /// cronômetros de abertura a entregar.
    /// </summary>
    public (TicketHistory History, IReadOnlyList<TicketSlaTimer> SlaTimers) RecordOpening(Guid openedByUserId)
    {
        TicketHistory history = CreateHistory(
            openedByUserId,
            TicketHistoryAction.Created,
            CreatedAtUtc,
            newValue: TicketStatus.Open.ToString());

        return (history, _openingSlaTimers);
    }

    /// <summary>
    /// Indica se o status pode ir para <paramref name="status"/>; o mesmo status não é uma transição.
    /// </summary>
    public bool CanTransitionTo(TicketStatus status)
    {
        if (status == Status)
        {
            return false;
        }

        return Status switch
        {
            TicketStatus.Open => status is TicketStatus.InProgress or TicketStatus.Canceled,
            TicketStatus.InProgress => status is TicketStatus.Resolved or TicketStatus.Canceled,
            TicketStatus.Resolved => status is TicketStatus.Closed or TicketStatus.Open,
            TicketStatus.Closed => status is TicketStatus.Open,
            TicketStatus.Canceled => status is TicketStatus.Open,
            _ => false
        };
    }

    public bool IsAssignedTo(Guid userId) => AssignedToUserId == userId;

    /// <summary>
    /// Situação do SLA de resolução no instante informado, sobre as colunas do chamado, para as consultas que não
    /// carregam a entidade. A primeira resolução decide o resultado (cumprido ou violado), mesmo depois de uma
    /// reabertura; chamado cancelado sem ter sido resolvido fica fora do SLA.
    /// </summary>
    public static TicketSlaStatus GetSlaStatus(
        TicketStatus status,
        DateTime dueAtUtc,
        DateTime slaWarningAtUtc,
        DateTime? resolvedAtUtc,
        DateTime utcNow)
    {
        if (resolvedAtUtc is { } resolvedAt)
        {
            return resolvedAt <= dueAtUtc ? TicketSlaStatus.Met : TicketSlaStatus.Missed;
        }

        if (status == TicketStatus.Canceled)
        {
            return TicketSlaStatus.NotApplicable;
        }

        if (utcNow >= dueAtUtc)
        {
            return TicketSlaStatus.Breached;
        }

        return utcNow >= slaWarningAtUtc ? TicketSlaStatus.AtRisk : TicketSlaStatus.OnTime;
    }

    /// <summary>
    /// Último instante em que o dono ainda pode cancelar o chamado aberto em <paramref name="createdAtUtc"/>; reabrir
    /// não reinicia o prazo.
    /// </summary>
    public static DateTime OwnerCancelDeadline(DateTime createdAtUtc) => createdAtUtc.AddMinutes(OwnerCancelWindowMinutes);

    /// <summary>
    /// Indica se o chamado pode ser cancelado agora, sobre as colunas dele: ativo, e por quem pode cancelar qualquer
    /// chamado ou pelo dono que pode editar o próprio, até o <see cref="OwnerCancelDeadline"/>.
    /// </summary>
    public static bool CanBeCanceled(
        TicketStatus status,
        DateTime createdAtUtc,
        bool canCancelAny,
        bool isOwnerWhoCanEdit,
        DateTime utcNow) =>
        status is TicketStatus.Open or TicketStatus.InProgress &&
        (canCancelAny || isOwnerWhoCanEdit && utcNow <= OwnerCancelDeadline(createdAtUtc));

    public bool IsParticipant(Guid userId) => CreatedByUserId == userId || IsAssignedTo(userId);

    /// <summary>
    /// Confere se o chamado pode ser cancelado agora (<see cref="CanBeCanceled"/>): por quem pode cancelar qualquer
    /// chamado, enquanto ele estiver ativo; pelo dono que pode editar o próprio chamado, só até o prazo dele, para
    /// desfazer um chamado aberto por engano. Falha com <see cref="TicketErrors.CancelWindowExpired"/> para o dono e com
    /// <see cref="TicketErrors.CancelForbidden"/> para os demais.
    /// </summary>
    public Result EnsureCanBeCanceled(bool canCancelAny, bool isOwnerWhoCanEdit, DateTime utcNow)
    {
        if (CanBeCanceled(Status, CreatedAtUtc, canCancelAny, isOwnerWhoCanEdit, utcNow))
        {
            return Result.Success();
        }

        return isOwnerWhoCanEdit
            ? Result.Failure(TicketErrors.CancelWindowExpired(OwnerCancelWindowMinutes))
            : Result.Failure(TicketErrors.CancelForbidden);
    }

    /// <summary>
    /// Troca a descrição e o status e dispara o evento de alteração, mesmo quando nada mudou. A troca de status também
    /// dispara o próprio evento, recalcula quem o chamado aguarda e atualiza os cronômetros recebidos deste chamado:
    /// resolver conclui o ciclo corrente de resolução e, se ainda estiver em aberto, a primeira resposta; cancelar para
    /// os em aberto. Devolve as entradas do histórico só do que mudou, o status antes da descrição. O handler já
    /// conferiu que o chamado está ativo e que a transição é permitida.
    /// </summary>
    public IReadOnlyList<TicketHistory> Update(
        string description,
        TicketStatus status,
        IReadOnlyCollection<TicketSlaTimer> slaTimers,
        bool lastMessageIsFromAgent,
        Guid changedByUserId,
        DateTime utcNow)
    {
        TicketStatus previousStatus = Status;
        string previousDescription = Description;
        var entries = new List<TicketHistory>();

        Description = description;
        Status = status;

        // O SLA é medido até a primeira resolução; reaberturas não mudam o resultado.
        if (status == TicketStatus.Resolved && ResolvedAtUtc is null)
        {
            ResolvedAtUtc = utcNow;
        }

        Raise(new TicketUpdatedDomainEvent(Id));

        if (status != previousStatus)
        {
            UpdateSlaTimers(status, slaTimers, utcNow);
            UpdateWaitingOn(lastMessageIsFromAgent);

            Raise(new TicketStatusChangedDomainEvent(Id, previousStatus, status, changedByUserId));

            entries.Add(CreateHistory(
                changedByUserId,
                TicketHistoryAction.StatusChanged,
                utcNow,
                previousStatus.ToString(),
                status.ToString()));
        }

        if (!string.Equals(previousDescription, description, StringComparison.Ordinal))
        {
            entries.Add(CreateHistory(
                changedByUserId,
                TicketHistoryAction.DescriptionChanged,
                utcNow,
                previousDescription,
                description));
        }

        return entries;
    }

    /// <summary>
    /// Troca o responsável. A primeira atribuição grava só o novo; a troca, o anterior e o novo. O handler já conferiu
    /// que o chamado está ativo e que o responsável muda.
    /// </summary>
    public TicketHistory Assign(User assignee, Guid changedByUserId, DateTime utcNow)
    {
        Guid? previousAssignedToUserId = AssignedToUserId;

        AssignedToUserId = assignee.Id;

        Raise(new TicketReassignedDomainEvent(Id, previousAssignedToUserId, assignee.Id, changedByUserId));

        return previousAssignedToUserId is null
            ? CreateHistory(changedByUserId, TicketHistoryAction.Assigned, utcNow, newValue: assignee.Id.ToString())
            : CreateHistory(
                changedByUserId,
                TicketHistoryAction.Reassigned,
                utcNow,
                previousAssignedToUserId.ToString(),
                assignee.Id.ToString());
    }

    /// <summary>
    /// Remove o responsável; o handler já conferiu que o chamado está ativo e tem responsável.
    /// </summary>
    public TicketHistory Unassign(Guid changedByUserId, DateTime utcNow)
    {
        Guid previousAssignedToUserId = AssignedToUserId!.Value;

        AssignedToUserId = null;

        Raise(new TicketUnassignedDomainEvent(Id, previousAssignedToUserId, changedByUserId));

        return CreateHistory(changedByUserId, TicketHistoryAction.Unassigned, utcNow, previousAssignedToUserId.ToString());
    }

    /// <summary>
    /// Fecha o chamado, que deixa de aguardar alguém; o handler já conferiu que a transição para Fechado é permitida.
    /// </summary>
    public TicketHistory Close(Guid changedByUserId, DateTime utcNow)
    {
        TicketStatus previousStatus = Status;

        Status = TicketStatus.Closed;

        UpdateWaitingOn(lastMessageIsFromAgent: false);

        Raise(new TicketStatusChangedDomainEvent(Id, previousStatus, TicketStatus.Closed, changedByUserId));

        return CreateHistory(
            changedByUserId,
            TicketHistoryAction.StatusChanged,
            utcNow,
            previousStatus.ToString(),
            TicketStatus.Closed.ToString());
    }

    /// <summary>
    /// Volta o chamado para Aberto, recalcula quem ele aguarda e abre um novo ciclo de resolução depois do último dos
    /// cronômetros recebidos deste chamado, com a meta integral do plano. Os ciclos anteriores, a primeira resposta, os
    /// prazos gravados no chamado (os do primeiro ciclo) e a data de resolução não mudam. Devolve a entrada do histórico
    /// e o cronômetro novo. O handler já conferiu que o chamado está inativo e que a transição para Aberto é permitida.
    /// </summary>
    public (TicketHistory History, TicketSlaTimer ResolutionTimer) Reopen(
        IReadOnlyCollection<TicketSlaTimer> slaTimers,
        BusinessCalendar calendar,
        bool lastMessageIsFromAgent,
        Guid changedByUserId,
        DateTime utcNow)
    {
        TicketStatus previousStatus = Status;

        Status = TicketStatus.Open;

        UpdateWaitingOn(lastMessageIsFromAgent);

        int lastResolutionCycle = slaTimers
            .Where(t => t.TicketId == Id && t.Metric == SlaMetric.Resolution)
            .Select(t => t.Cycle)
            .DefaultIfEmpty(0)
            .Max();

        var resolutionTimer = TicketSlaTimer.Create(
            Id,
            SlaMetric.Resolution,
            lastResolutionCycle + 1,
            SlaPlan.ResolutionMinutes,
            SlaPlan.CalendarId,
            calendar,
            utcNow);

        Raise(new TicketReopenedDomainEvent(Id, changedByUserId));

        TicketHistory history = CreateHistory(
            changedByUserId,
            TicketHistoryAction.Reopened,
            utcNow,
            previousStatus.ToString(),
            TicketStatus.Open.ToString());

        return (history, resolutionTimer);
    }

    /// <summary>
    /// Exclui o chamado logicamente e tira os cronômetros recebidos deste chamado da fila do monitoramento, sem violação
    /// nem evento; o handler já conferiu que ele está ativo.
    /// </summary>
    public TicketHistory Delete(IReadOnlyCollection<TicketSlaTimer> slaTimers, Guid changedByUserId, DateTime utcNow)
    {
        DeletedAtUtc = utcNow;

        foreach (TicketSlaTimer timer in slaTimers.Where(t => t.TicketId == Id))
        {
            timer.Dequeue();
        }

        Raise(new TicketDeletedDomainEvent(Id));

        return CreateHistory(changedByUserId, TicketHistoryAction.Deleted, utcNow);
    }

    /// <summary>
    /// Publica uma mensagem no chamado e devolve a mensagem e a entrada do histórico, que o handler adiciona antes de
    /// salvar. Leva a última atividade à data da mensagem e, com o chamado ativo, passa a aguardar o solicitante quando
    /// quem escreve atende a fila e não é o solicitante, ou quem atende nos demais casos.
    /// </summary>
    public (TicketMessage Message, TicketHistory History) PostMessage(
        Guid authorUserId,
        string content,
        bool authorIsAgent,
        DateTime utcNow)
    {
        var message = TicketMessage.Create(Id, authorUserId, content, utcNow);

        UpdateWaitingOn(authorIsAgent && authorUserId != CreatedByUserId);

        TicketHistory history = CreateHistory(authorUserId, TicketHistoryAction.MessageAdded, utcNow);

        return (message, history);
    }

    /// <summary>
    /// Leva a última atividade à data de uma entrada do histórico de anexo, sem nunca diminuí-la.
    /// </summary>
    public void RecordActivity(TicketHistory entry) => MoveLastActivity(entry.OccurredAtUtc);

    /// <summary>
    /// Leva a última atividade à data de uma entrada do histórico de mensagem editada ou excluída, sem nunca
    /// diminuí-la, e recalcula quem o chamado ativo aguarda: o solicitante quando a última mensagem válida é de quem
    /// atende, quem atende nos demais casos.
    /// </summary>
    public void RecordMessageActivity(TicketHistory entry, bool lastMessageIsFromAgent)
    {
        MoveLastActivity(entry.OccurredAtUtc);

        UpdateWaitingOn(lastMessageIsFromAgent);
    }

    // Só os cronômetros deste chamado mudam; o ciclo corrente de cada objetivo é o maior.
    private void UpdateSlaTimers(TicketStatus status, IReadOnlyCollection<TicketSlaTimer> slaTimers, DateTime utcNow)
    {
        TicketSlaTimer[] ownTimers = [.. slaTimers.Where(t => t.TicketId == Id)];

        if (status == TicketStatus.Canceled)
        {
            foreach (TicketSlaTimer timer in ownTimers)
            {
                timer.Stop(utcNow);
            }

            return;
        }

        if (status != TicketStatus.Resolved)
        {
            return;
        }

        ownTimers.Where(t => t.Metric == SlaMetric.Resolution).MaxBy(t => t.Cycle)
            ?.Complete(utcNow, SlaCompletionCause.Resolution);

        // Resolver sem ter respondido também conclui a primeira resposta, mas com a causa "resolução": a média de primeira
        // resposta conta só as concluídas por mensagem.
        ownTimers.Where(t => t.Metric == SlaMetric.FirstResponse).MaxBy(t => t.Cycle)
            ?.Complete(utcNow, SlaCompletionCause.Resolution);
    }

    // Só o chamado ativo aguarda alguém.
    private void UpdateWaitingOn(bool lastMessageIsFromAgent)
    {
        if (!IsActive)
        {
            WaitingOn = null;

            return;
        }

        WaitingOn = lastMessageIsFromAgent ? TicketWaitingOn.Requester : TicketWaitingOn.Agent;
    }

    private void MoveLastActivity(DateTime occurredAtUtc)
    {
        if (occurredAtUtc > LastActivityAtUtc)
        {
            LastActivityAtUtc = occurredAtUtc;
        }
    }

    private TicketHistory CreateHistory(
        Guid changedByUserId,
        TicketHistoryAction action,
        DateTime occurredAtUtc,
        string? oldValue = null,
        string? newValue = null)
    {
        MoveLastActivity(occurredAtUtc);

        return TicketHistory.Create(Id, changedByUserId, action, occurredAtUtc, oldValue, newValue);
    }
}
