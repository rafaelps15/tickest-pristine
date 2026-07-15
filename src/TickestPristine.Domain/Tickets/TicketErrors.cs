using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public static class TicketErrors
{
    public static Error NotFound(Guid ticketId) => Error.NotFound(
        "Tickets.NotFound",
        $"The ticket with the Id = '{ticketId}' was not found");

    public static Error NotActive(Guid ticketId) => Error.Problem(
        "Tickets.NotActive",
        $"The ticket with the Id = '{ticketId}' is not active");

    public static Error AlreadyActive(Guid ticketId) => Error.Problem(
        "Tickets.AlreadyActive",
        $"The ticket with the Id = '{ticketId}' is already active");

    public static Error InvalidStatusTransition(TicketStatus from, TicketStatus to) => Error.Problem(
        "Tickets.InvalidStatusTransition",
        $"Cannot transition a ticket from '{from}' to '{to}'");

    public static Error Unauthorized() => Error.Failure(
        "Tickets.Unauthorized",
        "You are not authorized to perform this action.");
}
