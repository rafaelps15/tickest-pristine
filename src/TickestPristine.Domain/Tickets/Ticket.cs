using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed class Ticket : Entity
{
    private Ticket(
        Guid id,
        string title,
        string description,
        TicketPriority priority,
        Guid createdByUserId,
        Guid? assignedToUserId,
        Guid sectorId,
        DateTime createdAtUtc)
    {
        Id = id;
        Title = title;
        Description = description;
        Priority = priority;
        Status = TicketStatus.Open;
        CreatedByUserId = createdByUserId;
        AssignedToUserId = assignedToUserId;
        SectorId = sectorId;
        CreatedAtUtc = createdAtUtc;
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
    public DateTime? DeletedAtUtc { get; private set; }

    public static Ticket Create(
        string title,
        string description,
        TicketPriority priority,
        Guid createdByUserId,
        Guid? assignedToUserId,
        Guid sectorId,
        DateTime createdAtUtc)
    {
        var ticket = new Ticket(Guid.NewGuid(), title, description, priority, createdByUserId, assignedToUserId, sectorId, createdAtUtc);

        ticket.Raise(new TicketCreatedDomainEvent(ticket.Id));

        return ticket;
    }

    public void Update(string description, TicketStatus status, Guid? changedByUserId = null)
    {
        TicketStatus previousStatus = Status;

        Description = description;
        Status = status;

        if (previousStatus != status)
        {
            Raise(new TicketStatusChangedDomainEvent(Id, previousStatus, status, changedByUserId));
        }
    }

    public void Reopen(Guid? reopenedByUserId = null)
    {
        Status = TicketStatus.Open;

        Raise(new TicketReopenedDomainEvent(Id, reopenedByUserId));
    }

    public void Delete(DateTime deletedAtUtc)
    {
        DeletedAtUtc = deletedAtUtc;

        Raise(new TicketDeletedDomainEvent(Id));
    }
}
