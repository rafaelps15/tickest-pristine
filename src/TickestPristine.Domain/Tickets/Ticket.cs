using System.Linq.Expressions;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.SlaCalendars;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.TicketHistories;
using TickestPristine.Domain.TicketMessages;

namespace TickestPristine.Domain.Tickets;

public sealed class Ticket : Entity, IAuditable
{
    public const int OwnerCancelWindowMinutes = 15;

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
    /// A regra de <see cref="IsActive"/> como filtro que o EF traduz para SQL, para os commands que procuram chamados
    /// ativos pelo contexto; as consultas em SQL usam o trecho com o mesmo nome da Application.
    /// </summary>
    public static Expression<Func<Ticket, bool>> Active { get; } =
        ticket => ticket.Status == TicketStatus.Open || ticket.Status == TicketStatus.InProgress;

    /// <summary>
    /// Abre o chamado com o plano de SLA e o prazo e o alerta de resolução do ciclo 1, no tempo útil do calendário do
    /// plano. Os cronômetros do ciclo 1 são criados pelo <see cref="TicketSlaCycleService.Start"/>. Sem mensagens, o
    /// chamado aguarda quem atende.
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
        SlaDeadlines resolution = TicketSlaPlan.CalculateDeadlines(slaPlan.ResolutionMinutes, calendar, utcNow);

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
            DueAtUtc = resolution.DueOnUtc,
            SlaWarningAtUtc = resolution.WarningOnUtc,
            SlaPlan = slaPlan
        };

        ticket.Raise(new TicketCreatedDomainEvent(ticket.Id, openedByUserId));

        return ticket;
    }

    /// <summary>
    /// Entrada de abertura do histórico, com a data de abertura; o handler a adiciona junto com o chamado.
    /// </summary>
    public TicketHistoryEntry RecordOpening(Guid openedByUserId) =>
        RecordHistory(
            openedByUserId,
            TicketHistoryAction.Created,
            CreatedAtUtc,
            newValue: TicketStatus.Open.ToString());

    /// <summary>
    /// Troca a descrição e dispara o evento com a anterior; devolve <c>null</c>, sem histórico nem evento, quando a
    /// descrição não muda. O handler já conferiu que o chamado está ativo.
    /// </summary>
    public TicketHistoryEntry? ChangeDescription(string description, Guid changedByUserId, DateTime utcNow)
    {
        if (string.Equals(Description, description, StringComparison.Ordinal))
        {
            return null;
        }

        string previousDescription = Description;

        Description = description;

        Raise(new TicketDescriptionChangedDomainEvent(Id, previousDescription));

        return RecordHistory(
            changedByUserId,
            TicketHistoryAction.DescriptionChanged,
            utcNow,
            previousDescription,
            description);
    }

    /// <summary>
    /// Troca o responsável. A primeira atribuição grava só o novo; a troca, o anterior e o novo. Falha com
    /// <see cref="TicketErrors.NotActive"/>, sem mudar nada, para o chamado inativo. O handler já conferiu que o
    /// responsável muda.
    /// </summary>
    public Result<TicketHistoryEntry> Assign(User assignee, Guid changedByUserId, DateTime utcNow)
    {
        if (!IsActive)
        {
            return Result.Failure<TicketHistoryEntry>(TicketErrors.NotActive(Id));
        }

        Guid? previousAssignedToUserId = AssignedToUserId;

        AssignedToUserId = assignee.Id;

        Raise(new TicketAssignedDomainEvent(Id, previousAssignedToUserId, assignee.Id, changedByUserId));

        TicketHistoryAction action = previousAssignedToUserId is null
            ? TicketHistoryAction.Assigned
            : TicketHistoryAction.Reassigned;

        return RecordHistory(
            changedByUserId,
            action,
            utcNow,
            previousAssignedToUserId?.ToString(),
            assignee.Id.ToString());
    }

    /// <summary>
    /// Remove o responsável. Falha com <see cref="TicketErrors.NotActive"/>, sem mudar nada, para o chamado inativo. O
    /// handler já conferiu que o chamado tem responsável.
    /// </summary>
    public Result<TicketHistoryEntry> Unassign(Guid changedByUserId, DateTime utcNow)
    {
        if (!IsActive)
        {
            return Result.Failure<TicketHistoryEntry>(TicketErrors.NotActive(Id));
        }

        Guid previousAssignedToUserId = AssignedToUserId!.Value;

        AssignedToUserId = null;

        Raise(new TicketUnassignedDomainEvent(Id, previousAssignedToUserId, changedByUserId));

        return RecordHistory(changedByUserId, TicketHistoryAction.Unassigned, utcNow, previousAssignedToUserId.ToString());
    }

    /// <summary>
    /// Publica uma mensagem no chamado e devolve a mensagem e a entrada do histórico, que o handler adiciona antes de
    /// salvar. Leva a última atividade à data da mensagem e, com o chamado ativo, passa a aguardar o solicitante quando
    /// quem escreve atende a fila e não é o solicitante, ou quem atende nos demais casos.
    /// </summary>
    public (TicketMessage Message, TicketHistoryEntry History) PostMessage(
        Guid authorUserId,
        string content,
        bool authorIsAgent,
        DateTime utcNow)
    {
        var message = TicketMessage.Create(Id, authorUserId, content, utcNow);

        RecalculateWaitingOn(authorIsAgent && authorUserId != CreatedByUserId);

        TicketHistoryEntry history = RecordHistory(authorUserId, TicketHistoryAction.MessageAdded, utcNow);

        return (message, history);
    }

    /// <summary>
    /// Leva a última atividade à data de uma entrada do histórico de anexo, sem nunca diminuí-la.
    /// </summary>
    public void RecordAttachmentActivity(TicketHistoryEntry entry) => AdvanceLastActivityTo(entry.OccurredAtUtc);

    /// <summary>
    /// Leva a última atividade à data de uma entrada do histórico de mensagem editada ou excluída, sem nunca
    /// diminuí-la, e recalcula quem o chamado ativo aguarda: o solicitante quando a última mensagem válida é de quem
    /// atende, quem atende nos demais casos.
    /// </summary>
    public void RecordMessageActivity(TicketHistoryEntry entry, bool lastMessageIsFromAgent)
    {
        AdvanceLastActivityTo(entry.OccurredAtUtc);

        RecalculateWaitingOn(lastMessageIsFromAgent);
    }

    /// <summary>
    /// Leva o chamado para Em andamento e recalcula quem ele aguarda. Falha com <see cref="TicketErrors.NotActive"/>
    /// para o chamado inativo e com <see cref="TicketErrors.InvalidStatusTransition"/> quando a transição não é
    /// permitida, sem mudar nada.
    /// </summary>
    public Result<TicketHistoryEntry> StartProgress(bool lastMessageIsFromAgent, Guid changedByUserId, DateTime utcNow)
    {
        Result transition = EnsureActiveTransition(TicketStatus.InProgress);

        if (transition.IsFailure)
        {
            return Result.Failure<TicketHistoryEntry>(transition.Error);
        }

        TicketHistoryEntry history = ChangeStatus(TicketStatus.InProgress, changedByUserId, utcNow);

        RecalculateWaitingOn(lastMessageIsFromAgent);

        return history;
    }

    /// <summary>
    /// Resolve o chamado, grava a data da primeira resolução e deixa de aguardar alguém. Falha com
    /// <see cref="TicketErrors.NotActive"/> para o chamado inativo e com <see cref="TicketErrors.InvalidStatusTransition"/>
    /// quando a transição não é permitida, sem mudar nada. Chamado só pelo <see cref="TicketSlaCycleService"/>, que fecha
    /// o ciclo de resolução.
    /// </summary>
    internal Result<TicketHistoryEntry> Resolve(Guid resolvedByUserId, DateTime utcNow)
    {
        Result transition = EnsureActiveTransition(TicketStatus.Resolved);

        if (transition.IsFailure)
        {
            return Result.Failure<TicketHistoryEntry>(transition.Error);
        }

        // O SLA é medido até a primeira resolução; reaberturas não mudam o resultado.
        ResolvedAtUtc ??= utcNow;

        return ChangeStatus(TicketStatus.Resolved, resolvedByUserId, utcNow);
    }

    /// <summary>
    /// Fecha o chamado, que deixa de aguardar alguém. Falha com <see cref="TicketErrors.InvalidStatusTransition"/>, sem
    /// mudar nada, quando a transição para Fechado não é permitida.
    /// </summary>
    public Result<TicketHistoryEntry> Close(Guid changedByUserId, DateTime utcNow)
    {
        if (!CanTransitionTo(TicketStatus.Closed))
        {
            return Result.Failure<TicketHistoryEntry>(TicketErrors.InvalidStatusTransition(Status, TicketStatus.Closed));
        }

        return ChangeStatus(TicketStatus.Closed, changedByUserId, utcNow);
    }

    /// <summary>
    /// Cancela o chamado, que deixa de aguardar alguém: por quem pode cancelar qualquer chamado, enquanto ele estiver
    /// ativo; pelo dono que pode editar o próprio chamado, só até o <see cref="OwnerCancelDeadline"/>, para desfazer um
    /// chamado aberto por engano. Falha, sem mudar nada, com <see cref="TicketErrors.NotActive"/> para o chamado inativo,
    /// com <see cref="TicketErrors.CancelWindowExpired"/> para o dono fora do prazo e com
    /// <see cref="TicketErrors.CancelForbidden"/> para os demais. Chamado só pelo <see cref="TicketSlaCycleService"/>, que
    /// para os cronômetros.
    /// </summary>
    internal Result<TicketHistoryEntry> Cancel(bool canCancelAny, bool isOwnerWhoCanEdit, Guid canceledByUserId, DateTime utcNow)
    {
        Result transition = EnsureActiveTransition(TicketStatus.Canceled);

        if (transition.IsFailure)
        {
            return Result.Failure<TicketHistoryEntry>(transition.Error);
        }

        if (!CanBeCanceled(Status, CreatedAtUtc, canCancelAny, isOwnerWhoCanEdit, utcNow))
        {
            return Result.Failure<TicketHistoryEntry>(isOwnerWhoCanEdit
                ? TicketErrors.CancelWindowExpired(OwnerCancelWindowMinutes)
                : TicketErrors.CancelForbidden);
        }

        return ChangeStatus(TicketStatus.Canceled, canceledByUserId, utcNow);
    }

    /// <summary>
    /// As conferências do <see cref="Reopen"/>, sem mudar nada, na ordem dele: <see cref="TicketErrors.AlreadyActive"/>,
    /// <see cref="TicketErrors.InvalidStatusTransition"/> e <see cref="TicketErrors.ResolutionCycleAlreadyOpen"/>. O
    /// <see cref="TicketSlaCycleService"/> as consulta antes de calcular o novo ciclo, que pode lançar exceção.
    /// </summary>
    internal Result EnsureCanReopen(bool hasOpenResolutionCycle)
    {
        if (IsActive)
        {
            return Result.Failure(TicketErrors.AlreadyActive(Id));
        }

        if (!CanTransitionTo(TicketStatus.Open))
        {
            return Result.Failure(TicketErrors.InvalidStatusTransition(Status, TicketStatus.Open));
        }

        return hasOpenResolutionCycle
            ? Result.Failure(TicketErrors.ResolutionCycleAlreadyOpen(Id))
            : Result.Success();
    }

    /// <summary>
    /// Volta o chamado para Aberto e recalcula quem ele aguarda; os prazos gravados no chamado (os do primeiro ciclo) e
    /// a data de resolução não mudam. Falha, sem mudar nada, com <see cref="TicketErrors.AlreadyActive"/> para o chamado
    /// ativo, com <see cref="TicketErrors.InvalidStatusTransition"/> quando a transição para Aberto não é permitida e com
    /// <see cref="TicketErrors.ResolutionCycleAlreadyOpen"/> quando o chamado ainda tem um ciclo de resolução aberto.
    /// Chamado só pelo <see cref="TicketSlaCycleService"/>, que abre o novo ciclo de resolução.
    /// </summary>
    internal Result<TicketHistoryEntry> Reopen(
        bool hasOpenResolutionCycle,
        bool lastMessageIsFromAgent,
        Guid reopenedByUserId,
        DateTime utcNow)
    {
        Result canReopen = EnsureCanReopen(hasOpenResolutionCycle);

        if (canReopen.IsFailure)
        {
            return Result.Failure<TicketHistoryEntry>(canReopen.Error);
        }

        TicketStatus previousStatus = Status;

        Status = TicketStatus.Open;

        RecalculateWaitingOn(lastMessageIsFromAgent);

        Raise(new TicketReopenedDomainEvent(Id, reopenedByUserId));

        return RecordHistory(
            reopenedByUserId,
            TicketHistoryAction.Reopened,
            utcNow,
            previousStatus.ToString(),
            TicketStatus.Open.ToString());
    }

    /// <summary>
    /// Exclui o chamado logicamente. Falha com <see cref="TicketErrors.NotActive"/>, sem mudar nada, para o chamado
    /// inativo. Chamado só pelo <see cref="TicketSlaCycleService"/>, que tira os cronômetros da fila.
    /// </summary>
    internal Result<TicketHistoryEntry> Delete(Guid deletedByUserId, DateTime utcNow)
    {
        if (!IsActive)
        {
            return Result.Failure<TicketHistoryEntry>(TicketErrors.NotActive(Id));
        }

        DeletedAtUtc = utcNow;

        Raise(new TicketDeletedDomainEvent(Id));

        return RecordHistory(deletedByUserId, TicketHistoryAction.Deleted, utcNow);
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

    public bool IsParticipant(Guid userId) => CreatedByUserId == userId || IsAssignedTo(userId);

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

    // A ordem das conferências é a dos handlers: primeiro o chamado ativo, depois a transição.
    private Result EnsureActiveTransition(TicketStatus status)
    {
        if (!IsActive)
        {
            return Result.Failure(TicketErrors.NotActive(Id));
        }

        return CanTransitionTo(status)
            ? Result.Success()
            : Result.Failure(TicketErrors.InvalidStatusTransition(Status, status));
    }

    // Chamado depois das conferências: troca o status, dispara o evento e grava o histórico. O chamado que fica inativo
    // deixa de aguardar alguém; quem leva a um status ativo recalcula quem ele aguarda.
    private TicketHistoryEntry ChangeStatus(TicketStatus status, Guid changedByUserId, DateTime utcNow)
    {
        TicketStatus previousStatus = Status;

        Status = status;

        if (!IsActive)
        {
            WaitingOn = null;
        }

        Raise(new TicketStatusChangedDomainEvent(Id, previousStatus, status, changedByUserId));

        return RecordHistory(
            changedByUserId,
            TicketHistoryAction.StatusChanged,
            utcNow,
            previousStatus.ToString(),
            status.ToString());
    }

    // Só o chamado ativo aguarda alguém: o solicitante quando a última mensagem é de quem atende, quem atende nos demais
    // casos.
    private void RecalculateWaitingOn(bool lastMessageIsFromAgent)
    {
        if (!IsActive)
        {
            WaitingOn = null;

            return;
        }

        WaitingOn = lastMessageIsFromAgent ? TicketWaitingOn.Requester : TicketWaitingOn.Agent;
    }

    // A última atividade só avança: uma data anterior à atual não a muda.
    private void AdvanceLastActivityTo(DateTime occurredAtUtc)
    {
        if (occurredAtUtc > LastActivityAtUtc)
        {
            LastActivityAtUtc = occurredAtUtc;
        }
    }

    // Toda entrada do histórico leva a última atividade à data dela.
    private TicketHistoryEntry RecordHistory(
        Guid changedByUserId,
        TicketHistoryAction action,
        DateTime occurredAtUtc,
        string? oldValue = null,
        string? newValue = null)
    {
        AdvanceLastActivityTo(occurredAtUtc);

        return TicketHistoryEntry.Create(Id, changedByUserId, action, occurredAtUtc, oldValue, newValue);
    }
}
