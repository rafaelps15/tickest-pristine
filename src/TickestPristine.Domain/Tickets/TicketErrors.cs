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

    public static Error AlreadyActive(Guid ticketId) => Error.Problem(
        "Tickets.AlreadyActive",
        $"O chamado com o Id = '{ticketId}' já está ativo");

    public static Error InvalidStatusTransition(TicketStatus from, TicketStatus to) => Error.Problem(
        "Tickets.InvalidStatusTransition",
        $"Não é possível alterar o status do chamado de '{TicketStatusNames.GetDisplayName(from)}' para '{TicketStatusNames.GetDisplayName(to)}'");

    public static Error Unauthorized() => Error.Forbidden(
        "Tickets.Unauthorized",
        "Você não tem permissão para executar esta ação.");
}
