namespace TickestPristine.Domain.Tickets;

public enum TicketHistoryAction
{
    Created = 1,
    StatusChanged = 2,
    Reassigned = 3,
    Reopened = 4,
    Deleted = 5,
    MessageAdded = 6,
    AttachmentAdded = 7,
    AttachmentRemoved = 8,
    MessageEdited = 9,
    MessageRemoved = 10
}
