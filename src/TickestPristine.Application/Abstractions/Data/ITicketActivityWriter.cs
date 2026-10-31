using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Abstractions.Data;

/// <summary>
/// Grava, depois do commit de uma ação sobre uma mensagem ou um anexo, a última atividade e a situação de espera que o
/// chamado calculou: só essas colunas e sem o token de concorrência do chamado, para a ação não alterar a versão dele e
/// duas ações simultâneas no mesmo chamado não conflitarem.
/// </summary>
public interface ITicketActivityWriter
{
    /// <summary>
    /// Leva a última atividade gravada à do chamado, sem nunca diminuí-la.
    /// </summary>
    Task WriteLastActivityAsync(Ticket ticket, CancellationToken cancellationToken);

    /// <summary>
    /// Leva a última atividade gravada à do chamado, sem nunca diminuí-la, e grava a situação de espera dele; o chamado
    /// que deixou de estar ativo no banco fica sem situação.
    /// </summary>
    Task WriteLastActivityAndWaitingOnAsync(Ticket ticket, CancellationToken cancellationToken);
}
