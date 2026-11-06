using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.TicketMessages;

public static class TicketMessageErrors
{
    public static Error NotFound(Guid messageId) => Error.NotFound(
        "TicketMessages.NotFound",
        $"A mensagem com o Id = '{messageId}' não foi encontrada");
}
