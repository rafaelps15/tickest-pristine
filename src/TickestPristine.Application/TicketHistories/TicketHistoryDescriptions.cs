using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.TicketHistories;

/// <summary>
/// Texto exibido ao usuário para cada registro do histórico de chamados, montado na leitura a partir da ação e dos
/// valores gravados. O banco guarda só os fatos, então mudar um texto aqui vale também para os registros antigos.
/// </summary>
internal static class TicketHistoryDescriptions
{
    public static string For(TicketHistoryAction action, string? oldValue, string? newValue) =>
        action switch
        {
            TicketHistoryAction.Created => "Chamado criado",
            TicketHistoryAction.StatusChanged =>
                $"Status alterado de {GetStatusDisplayName(oldValue)} para {GetStatusDisplayName(newValue)}",
            TicketHistoryAction.Reassigned => "Responsável alterado",
            TicketHistoryAction.Unassigned => "Responsável removido",
            TicketHistoryAction.Reopened => "Chamado reaberto",
            TicketHistoryAction.Deleted => "Chamado excluído",
            TicketHistoryAction.MessageAdded => "Nova mensagem adicionada ao chamado",
            TicketHistoryAction.MessageEdited => "Mensagem editada",
            TicketHistoryAction.MessageRemoved => "Mensagem removida",
            TicketHistoryAction.AttachmentAdded => $"Anexo '{newValue}' adicionado",
            TicketHistoryAction.AttachmentRemoved => "Anexo removido",
            _ => action.ToString()
        };

    /// <summary>
    /// O status é gravado pelo nome do enum (ex.: "InProgress"); um valor que não seja status volta como veio.
    /// </summary>
    private static string GetStatusDisplayName(string? value) =>
        Enum.TryParse(value, out TicketStatus status) && Enum.IsDefined(status)
            ? TicketStatusNames.GetDisplayName(status)
            : value ?? string.Empty;
}
