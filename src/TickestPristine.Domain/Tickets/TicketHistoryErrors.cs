using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public static class TicketHistoryErrors
{
    public static Error NotFound(Guid historyId) => Error.NotFound(
        "TicketHistories.NotFound",
        $"O registro de histórico com o Id = '{historyId}' não foi encontrado");
}
