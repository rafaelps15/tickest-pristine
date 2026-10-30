using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed class Ticket : Entity, IAuditable
{
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

    // Data da ação mais recente do histórico (na abertura, a própria abertura); nunca volta para uma data anterior.
    public DateTime LastActivityAtUtc { get; private set; }

    // Calculado no banco a cada mensagem e troca de status; nulo enquanto o chamado está inativo.
    public TicketWaitingOn? WaitingOn { get; }

    public DateTime DueAtUtc { get; private set; }

    public DateTime SlaWarningAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    public TicketSlaPlan SlaPlan { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    public bool IsActive => IsActiveStatus(Status);

    public static bool IsActiveStatus(TicketStatus status) => status is TicketStatus.Open or TicketStatus.InProgress;

    public static Ticket Create(
        string title,
        string description,
        TicketPriority priority,
        Sector sector,
        User requester,
        User? assignee,
        TicketSlaPlan slaPlan,
        DateTime dueAtUtc,
        DateTime slaWarningAtUtc,
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
            DueAtUtc = dueAtUtc,
            SlaWarningAtUtc = slaWarningAtUtc,
            SlaPlan = slaPlan
        };

        ticket.Raise(new TicketCreatedDomainEvent(ticket.Id, openedByUserId));

        return ticket;
    }

    /// <summary>
    /// Entrada de abertura do histórico, com a data de abertura; o chamado sempre nasce com o status Aberto.
    /// </summary>
    public TicketHistory RecordOpening(Guid openedByUserId) =>
        CreateHistory(openedByUserId, TicketHistoryAction.Created, CreatedAtUtc, newValue: TicketStatus.Open.ToString());

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
    /// Troca a descrição e o status e dispara o evento de alteração, mesmo quando nada mudou; a troca de status também
    /// dispara o próprio evento. Devolve as entradas do histórico só do que mudou, o status antes da descrição. O
    /// handler já conferiu que o chamado está ativo e que a transição é permitida.
    /// </summary>
    public IReadOnlyList<TicketHistory> Update(string description, TicketStatus status, Guid changedByUserId, DateTime utcNow)
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
    /// Fecha o chamado; o handler já conferiu que a transição para Fechado é permitida.
    /// </summary>
    public TicketHistory Close(Guid changedByUserId, DateTime utcNow)
    {
        TicketStatus previousStatus = Status;

        Status = TicketStatus.Closed;

        Raise(new TicketStatusChangedDomainEvent(Id, previousStatus, TicketStatus.Closed, changedByUserId));

        return CreateHistory(
            changedByUserId,
            TicketHistoryAction.StatusChanged,
            utcNow,
            previousStatus.ToString(),
            TicketStatus.Closed.ToString());
    }

    /// <summary>
    /// Volta o chamado para Aberto; os prazos gravados (os do primeiro ciclo) e a data de resolução não mudam. O handler
    /// já conferiu que o chamado está inativo e que a transição para Aberto é permitida.
    /// </summary>
    public TicketHistory Reopen(Guid changedByUserId, DateTime utcNow)
    {
        TicketStatus previousStatus = Status;

        Status = TicketStatus.Open;

        Raise(new TicketReopenedDomainEvent(Id, changedByUserId));

        return CreateHistory(
            changedByUserId,
            TicketHistoryAction.Reopened,
            utcNow,
            previousStatus.ToString(),
            TicketStatus.Open.ToString());
    }

    /// <summary>
    /// Exclui o chamado logicamente; o handler já conferiu que ele está ativo.
    /// </summary>
    public TicketHistory Delete(Guid changedByUserId, DateTime utcNow)
    {
        DeletedAtUtc = utcNow;

        Raise(new TicketDeletedDomainEvent(Id));

        return CreateHistory(changedByUserId, TicketHistoryAction.Deleted, utcNow);
    }

    private TicketHistory CreateHistory(
        Guid changedByUserId,
        TicketHistoryAction action,
        DateTime occurredAtUtc,
        string? oldValue = null,
        string? newValue = null) => new()
    {
        Id = Guid.NewGuid(),
        TicketId = Id,
        ChangedByUserId = changedByUserId,
        Action = action,
        OldValue = oldValue,
        NewValue = newValue,
        OccurredAtUtc = occurredAtUtc
    };
}
