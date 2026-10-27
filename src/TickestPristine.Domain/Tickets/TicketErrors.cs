using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public static class TicketErrors
{
    public static Error NotFound(Guid ticketId) => Error.NotFound(
        "Tickets.NotFound",
        $"O chamado com o Id = '{ticketId}' não foi encontrado");

    public static Error NotActive(Guid ticketId) => Error.Problem(
        "Tickets.NotActive",
        $"O chamado com o Id = '{ticketId}' não está ativo");

    public static Error AlreadyActive(Guid ticketId) => Error.Conflict(
        "Tickets.AlreadyActive",
        $"O chamado com o Id = '{ticketId}' já está ativo");

    public static Error InvalidStatusTransition(TicketStatus from, TicketStatus to) => Error.Problem(
        "Tickets.InvalidStatusTransition",
        $"Não é possível alterar o status do chamado de '{TicketStatusNames.GetDisplayName(from)}' para '{TicketStatusNames.GetDisplayName(to)}'");

    public static Error AssigneeNotAttendant(Guid userId) => Error.Problem(
        "Tickets.AssigneeNotAttendant",
        $"O usuário com o Id = '{userId}' não atende chamados e não pode ser o responsável");

    public static readonly Error CannotAssignToOthers = Error.Forbidden(
        "Tickets.CannotAssignToOthers",
        "Você só pode atribuir o chamado a você mesmo.");
}
