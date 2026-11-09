using System.Linq.Expressions;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.SlaCalendars;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.TicketAttachments;
using TickestPristine.Domain.TicketHistories;
using TickestPristine.Domain.TicketMessages;

namespace TickestPristine.Domain.Tickets;

public sealed class Ticket : Entity, IAuditable
{
    public const int OwnerCancelWindowMinutes = 15;

    private readonly List<TicketSlaTimer> _slaTimers = [];

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

    public IReadOnlyCollection<TicketSlaTimer> SlaTimers => _slaTimers;

    /// <summary>
    /// A regra de <see cref="IsActive"/> como filtro que o EF traduz para SQL, para os commands que procuram chamados
    /// ativos pelo contexto; as consultas em SQL usam o trecho com o mesmo nome da Application.
    /// </summary>
    public static Expression<Func<Ticket, bool>> Active { get; } =
        ticket => ticket.Status == TicketStatus.Open || ticket.Status == TicketStatus.InProgress;

    /// <summary>Abre o chamado e cria os cronômetros do primeiro ciclo no tempo útil do calendário.</summary>
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
        SlaDeadlines? firstResponse = slaPlan.FirstResponseMinutes is { } firstResponseMinutes
            ? TicketSlaPlan.CalculateDeadlines(firstResponseMinutes, calendar, utcNow)
            : null;

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

        ticket._slaTimers.Add(TicketSlaTimer.Create(
            ticket.Id,
            SlaMetric.Resolution,
            1,
            slaPlan.ResolutionMinutes,
            slaPlan.CalendarId,
            utcNow,
            resolution));

        if (firstResponse is { } firstResponseDeadlines)
        {
            ticket._slaTimers.Add(TicketSlaTimer.Create(
                ticket.Id,
                SlaMetric.FirstResponse,
                1,
                slaPlan.FirstResponseMinutes!.Value,
                slaPlan.CalendarId,
                utcNow,
                firstResponseDeadlines));
        }

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
    /// Publica uma mensagem e conclui a primeira resposta quando o autor é atendente.
    /// </summary>
    public TicketMessagePosting PostMessage(Guid authorUserId, string content, bool authorIsAgent, DateTime utcNow)
    {
        var message = TicketMessage.Create(Id, authorUserId, content, utcNow);
        var history = TicketHistoryEntry.Create(Id, authorUserId, TicketHistoryAction.MessageAdded, utcNow);

        if (IsAgentReply(authorUserId, authorIsAgent))
        {
            _slaTimers.FirstOrDefault(timer => timer.Metric == SlaMetric.FirstResponse)?.Complete(
                utcNow,
                SlaCompletionCause.Message);
        }

        return new TicketMessagePosting(message, history);
    }

    public TicketMessagePosting PostMessage(Guid authorUserId, string content, DateTime utcNow) =>
        PostMessage(authorUserId, content, authorIsAgent: false, utcNow);

    /// <summary>
    /// Anexa um arquivo ao chamado e devolve o anexo e a entrada do histórico, que o handler adiciona antes de salvar.
    /// Recusa, nesta ordem e sem mudar nada: o conteúdo que não confere com o tipo declarado, o arquivo fora da política
    /// (<see cref="TicketAttachmentPolicy.EnsureAccepted"/>) e o chamado que já tem, nos anexos não excluídos, o máximo de
    /// anexos ou de bytes somados. O chamado não muda: a última atividade sai da projeção, depois do commit.
    /// </summary>
    public Result<TicketAttachmentUpload> AddAttachment(
        Guid uploadedByUserId,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string storageKey,
        bool contentMatches,
        int currentCount,
        long currentTotalBytes,
        DateTime utcNow)
    {
        if (!contentMatches)
        {
            return Result.Failure<TicketAttachmentUpload>(TicketAttachmentErrors.ContentMismatch);
        }

        Result accepted = TicketAttachmentPolicy.EnsureAccepted(fileName, contentType, fileSizeBytes);

        if (accepted.IsFailure)
        {
            return Result.Failure<TicketAttachmentUpload>(accepted.Error);
        }

        if (currentCount >= TicketAttachmentPolicy.MaxAttachmentsPerTicket ||
            currentTotalBytes + fileSizeBytes > TicketAttachmentPolicy.MaxTotalBytesPerTicket)
        {
            return Result.Failure<TicketAttachmentUpload>(TicketAttachmentErrors.TicketLimitReached(
                TicketAttachmentPolicy.MaxAttachmentsPerTicket,
                TicketAttachmentPolicy.MaxTotalBytesPerTicket / (1024 * 1024)));
        }

        var attachment = TicketAttachment.Create(Id, uploadedByUserId, fileName, contentType, fileSizeBytes, storageKey, utcNow);
        var history = TicketHistoryEntry.Create(
            Id,
            uploadedByUserId,
            TicketHistoryAction.AttachmentAdded,
            utcNow,
            newValue: fileName);

        return new TicketAttachmentUpload(attachment, history);
    }

    /// <summary>
    /// Se a mensagem do autor conta como resposta de quem atende: ele atende a fila e não é o solicitante.
    /// </summary>
    public bool IsAgentReply(Guid authorUserId, bool authorIsAgent) => authorIsAgent && authorUserId != CreatedByUserId;

    /// <summary>
    /// Quem o chamado aguarda depois da última mensagem válida, sem mudar o chamado: ninguém (nulo) com o chamado
    /// inativo; o solicitante quando a mensagem é de quem atende; quem atende nos demais casos.
    /// </summary>
    public TicketWaitingOn? WaitingOnAfter(bool lastMessageIsFromAgent)
    {
        if (!IsActive)
        {
            return null;
        }

        return lastMessageIsFromAgent ? TicketWaitingOn.Requester : TicketWaitingOn.Agent;
    }

    /// <summary>
    /// Leva o chamado para Em andamento e recalcula quem ele aguarda. Falha com <see cref="TicketErrors.NotActive"/>
    /// para o chamado inativo e com <see cref="TicketErrors.InvalidStatusTransition"/> quando a transição não é
    /// permitida, sem mudar nada.
    /// </summary>
    public Result<TicketHistoryEntry> StartProgress(bool lastMessageIsFromAgent, Guid changedByUserId, DateTime utcNow)
    {
        Result transition = EnsureCanChangeStatusTo(TicketStatus.InProgress);

        if (transition.IsFailure)
        {
            return Result.Failure<TicketHistoryEntry>(transition.Error);
        }

        TicketHistoryEntry history = ChangeStatus(TicketStatus.InProgress, changedByUserId, utcNow);

        RecalculateWaitingOn(lastMessageIsFromAgent);

        return history;
    }

    /// <summary>Resolve o chamado e conclui os cronômetros abertos do ciclo corrente.</summary>
    public Result<TicketHistoryEntry> Resolve(Guid resolvedByUserId, DateTime utcNow)
    {
        Result transition = EnsureCanChangeStatusTo(TicketStatus.Resolved);

        if (transition.IsFailure)
        {
            return Result.Failure<TicketHistoryEntry>(transition.Error);
        }

        TicketSlaTimer? currentResolution = _slaTimers
            .Where(timer => timer.Metric == SlaMetric.Resolution)
            .MaxBy(timer => timer.Cycle);

        TicketSlaTimer? firstResponse = _slaTimers.FirstOrDefault(timer => timer.Metric == SlaMetric.FirstResponse);

        currentResolution?.Complete(utcNow, SlaCompletionCause.Resolution);
        firstResponse?.Complete(utcNow, SlaCompletionCause.Resolution);

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

    /// <summary>Cancela o chamado e para os cronômetros abertos.</summary>
    public Result<TicketHistoryEntry> Cancel(bool canCancelAny, bool isOwnerWhoCanEdit, Guid canceledByUserId, DateTime utcNow)
    {
        Result transition = EnsureCanChangeStatusTo(TicketStatus.Canceled);

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

        TicketHistoryEntry history = ChangeStatus(TicketStatus.Canceled, canceledByUserId, utcNow);

        foreach (TicketSlaTimer timer in _slaTimers)
        {
            timer.Stop(utcNow);
        }

        return history;
    }

    /// <summary>Confere se o chamado pode ser reaberto sem alterar seu estado.</summary>
    public Result EnsureCanReopen()
    {
        if (IsActive)
        {
            return Result.Failure(TicketErrors.AlreadyActive(Id));
        }

        if (!CanTransitionTo(TicketStatus.Open))
        {
            return Result.Failure(TicketErrors.InvalidStatusTransition(Status, TicketStatus.Open));
        }

        return _slaTimers.Any(timer => timer.Metric == SlaMetric.Resolution && timer.IsOpen)
            ? Result.Failure(TicketErrors.ResolutionCycleAlreadyOpen(Id))
            : Result.Success();
    }

    /// <summary>Reabre o chamado e cria o próximo ciclo de resolução.</summary>
    public Result<TicketHistoryEntry> Reopen(
        BusinessCalendar calendar,
        bool lastMessageIsFromAgent,
        Guid reopenedByUserId,
        DateTime utcNow)
    {
        Result canReopen = EnsureCanReopen();

        if (canReopen.IsFailure)
        {
            return Result.Failure<TicketHistoryEntry>(canReopen.Error);
        }

        TicketSlaTimer? currentResolution = _slaTimers
            .Where(timer => timer.Metric == SlaMetric.Resolution)
            .MaxBy(timer => timer.Cycle);
        TicketSlaPlan plan = SlaPlan;
        SlaDeadlines deadlines = TicketSlaPlan.CalculateDeadlines(plan.ResolutionMinutes, calendar, utcNow);
        var resolutionCycle = TicketSlaTimer.Create(
            Id,
            SlaMetric.Resolution,
            currentResolution?.Cycle + 1 ?? 1,
            plan.ResolutionMinutes,
            plan.CalendarId,
            utcNow,
            deadlines);

        TicketStatus previousStatus = Status;

        Status = TicketStatus.Open;

        RecalculateWaitingOn(lastMessageIsFromAgent);

        Raise(new TicketReopenedDomainEvent(Id, reopenedByUserId));

        _slaTimers.Add(resolutionCycle);

        return RecordHistory(
            reopenedByUserId,
            TicketHistoryAction.Reopened,
            utcNow,
            previousStatus.ToString(),
            TicketStatus.Open.ToString());
    }

    /// <summary>Exclui o chamado logicamente e remove seus cronômetros da fila.</summary>
    public Result<TicketHistoryEntry> Delete(Guid deletedByUserId, DateTime utcNow)
    {
        if (!IsActive)
        {
            return Result.Failure<TicketHistoryEntry>(TicketErrors.NotActive(Id));
        }

        DeletedAtUtc = utcNow;

        foreach (TicketSlaTimer timer in _slaTimers.Where(timer => timer.NextActionOnUtc is not null))
        {
            timer.Dequeue();
        }

        Raise(new TicketDeletedDomainEvent(Id));

        return RecordHistory(deletedByUserId, TicketHistoryAction.Deleted, utcNow);
    }

    /// <summary>Apura um cronômetro deste chamado e devolve se uma violação foi registrada.</summary>
    public bool SettleSlaDeadline(Guid slaTimerId) =>
        _slaTimers.FirstOrDefault(timer => timer.Id == slaTimerId)?.SettleDeadline() is true;

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

    /// <summary>
    /// Se o chamado pode ir para <paramref name="status"/>, sem mudar nada: falha com <see cref="TicketErrors.NotActive"/>
    /// para o chamado inativo e com <see cref="TicketErrors.InvalidStatusTransition"/> quando a transição não é permitida.
    /// </summary>
    public Result EnsureCanChangeStatusTo(TicketStatus status)
    {
        if (!IsActive)
        {
            return Result.Failure(TicketErrors.NotActive(Id));
        }

        return CanTransitionTo(status)
            ? Result.Success()
            : Result.Failure(TicketErrors.InvalidStatusTransition(Status, status));
    }

    public bool IsAssignedTo(Guid userId) => AssignedToUserId == userId;

    public bool IsParticipant(Guid userId) => CreatedByUserId == userId || IsAssignedTo(userId);

    /// <summary>
    /// Se o usuário pode ver o chamado e agir nele: os participantes (solicitante e responsável) sempre podem; os
    /// demais, só com <c>tickets:manage</c>, que o handler confere e passa em <paramref name="canManageTickets"/>.
    /// </summary>
    public bool CanBeViewedBy(Guid userId, bool canManageTickets) => canManageTickets || IsParticipant(userId);

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

    // A mesma regra que o WaitingOnAfter responde, agora gravada no próprio chamado.
    private void RecalculateWaitingOn(bool lastMessageIsFromAgent) => WaitingOn = WaitingOnAfter(lastMessageIsFromAgent);

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
