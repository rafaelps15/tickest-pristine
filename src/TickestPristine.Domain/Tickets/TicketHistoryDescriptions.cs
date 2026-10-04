namespace TickestPristine.Domain.Tickets;

/// <summary>
/// Textos do histórico de chamados exibidos ao usuário, num lugar só: a API e os dados de exemplo gravam o mesmo texto.
/// </summary>
public static class TicketHistoryDescriptions
{
    public const string Created = "Chamado criado";

    public const string Deleted = "Chamado excluído";

    public const string Reopened = "Chamado reaberto";

    public const string MessagePosted = "Nova mensagem adicionada ao chamado";

    public const string MessageEdited = "Mensagem editada";

    public const string MessageRemoved = "Mensagem removida";

    public const string AttachmentRemoved = "Anexo removido";

    public static string AttachmentAdded(string fileName) => $"Anexo '{fileName}' adicionado";

    public static string StatusChanged(TicketStatus oldStatus, TicketStatus newStatus) =>
        $"Status alterado de {TicketStatusNames.GetDisplayName(oldStatus)} para {TicketStatusNames.GetDisplayName(newStatus)}";
}
