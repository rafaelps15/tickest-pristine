using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public static class TicketMessageErrors
{
    public static Error NotFound(Guid messageId) => Error.NotFound(
        "TicketMessages.NotFound",
        $"A mensagem com o Id = '{messageId}' não foi encontrada");

    public static Error Unauthorized() => Error.Forbidden(
        "TicketMessages.Unauthorized",
        "Você não tem permissão para executar esta ação nesta mensagem.");
}
