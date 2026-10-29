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

    public static Error CancelWindowExpired(int windowInMinutes) => Error.Forbidden(
        "Tickets.CancelWindowExpired",
        $"O prazo de {windowInMinutes} minutos para cancelar o próprio chamado terminou. Peça o cancelamento a quem administra os chamados.");

    public static readonly Error CancelForbidden = Error.Forbidden(
        "Tickets.CancelForbidden",
        "Você não tem permissão para cancelar este chamado.");

    public static readonly Error StatusChangeForbidden = Error.Forbidden(
        "Tickets.StatusChangeForbidden",
        "Só quem atende a fila de chamados pode mudar o status para Em Andamento ou Resolvido.");

    public static readonly Error StatsScopeForbidden = Error.Forbidden(
        "Tickets.StatsScopeForbidden",
        "Só quem atende a fila de chamados pode filtrar as estatísticas por setor, prioridade, solicitante ou responsável.");

    public static readonly Error InvalidPeriod = Error.Problem(
        "Tickets.InvalidPeriod",
        "A data inicial do período não pode ser posterior à data final.");

    public static Error VolumeTooManyPoints(int maxPoints) => Error.Problem(
        "Tickets.VolumeTooManyPoints",
        $"O período gera mais de {maxPoints} pontos no gráfico. Escolha um período menor ou um agrupamento maior.");
}
