using System.Diagnostics;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.TicketHistories;

namespace TickestPristine.Application.Tickets.Update;

/// <summary>
/// Atualiza o chamado pelo <c>PUT</c>: troca a descrição e, no mesmo pedido, pode levá-lo para Em andamento, Resolvido
/// ou Cancelado. Confere, nesta ordem: a permissão da intenção, o chamado ativo, a transição e, nas trocas de status que
/// não cancelam, <c>tickets:manage</c>. Uma falha não muda nada.
/// </summary>
internal sealed class UpdateTicketCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    TicketAgentLookup agentLookup,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateTicketCommand>
{
    public async Task<Result> Handle(UpdateTicketCommand command, CancellationToken cancellationToken)
    {
        Ticket? ticket = await context.Tickets
            .Include(t => t.SlaTimers)
            .SingleOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure(TicketErrors.NotFound(command.TicketId));
        }

        UpdateIntent intent = ResolveIntent(ticket, command);

        if (!await HasPermissionToUpdateAsync(ticket, intent, cancellationToken))
        {
            return Result.Failure(UserErrors.Unauthorized());
        }

        if (!ticket.IsActive)
        {
            return Result.Failure(TicketErrors.NotActive(ticket.Id));
        }

        DateTime nowUtc = dateTimeProvider.UtcNow;
        var historyEntries = new List<TicketHistoryEntry>();

        // O status antes da descrição, como no histórico de antes; uma falha não muda nada.
        if (intent.ChangesStatus)
        {
            Result<TicketHistoryEntry> statusChange = await ChangeStatusAsync(ticket, command.Status, intent, nowUtc, cancellationToken);

            if (statusChange.IsFailure)
            {
                return statusChange;
            }

            historyEntries.Add(statusChange.Value);
        }

        if (ticket.ChangeDescription(command.Description, userContext.UserId, nowUtc) is { } descriptionChange)
        {
            historyEntries.Add(descriptionChange);
        }

        context.TicketHistories.AddRange(historyEntries);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static UpdateIntent ResolveIntent(Ticket ticket, UpdateTicketCommand command)
    {
        bool changesStatus = command.Status != ticket.Status;

        return new UpdateIntent(
            ChangesStatus: changesStatus,
            Cancels: changesStatus && command.Status == TicketStatus.Canceled,
            ChangesDescription: !string.Equals(ticket.Description, command.Description, StringComparison.Ordinal));
    }

    private async Task<Result<TicketHistoryEntry>> ChangeStatusAsync(
        Ticket ticket,
        TicketStatus status,
        UpdateIntent intent,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        Result canChange = ticket.EnsureCanChangeStatusTo(status);

        if (canChange.IsFailure)
        {
            return Result.Failure<TicketHistoryEntry>(canChange.Error);
        }

        // Cancelar segue as regras do Ticket (janela do dono e tickets:cancel);
        // as demais trocas (Em andamento e Resolvido) exigem tickets:manage: o dono só edita a descrição e cancela.
        if (!intent.Cancels && !await HasPermissionToManageAsync(cancellationToken))
        {
            return Result.Failure<TicketHistoryEntry>(TicketErrors.StatusChangeForbidden);
        }

        // Os cronômetros só mudam com o status; quem o chamado aguarda é recalculado na troca de status.
        bool lastMessageIsFromAgent = await agentLookup.IsLastValidMessageFromAgentAsync(ticket, cancellationToken);

        Guid callerId = userContext.UserId;

        return status switch
        {
            TicketStatus.InProgress => ticket.StartProgress(lastMessageIsFromAgent, callerId, nowUtc),
            TicketStatus.Resolved => ticket.Resolve(callerId, nowUtc),
            TicketStatus.Canceled => ticket.Cancel(
                await HasPermissionToCancelAnyAsync(cancellationToken),
                await HasPermissionToEditOwnAsync(ticket, cancellationToken),
                callerId,
                nowUtc),
            _ => throw new UnreachableException($"O chamado já recusou a transição para {status}.")
        };
    }

    // Editar a descrição é do dono com tickets:update-own e de quem atende a fila; quem só tem tickets:cancel passa apenas
    // para cancelar, sem mudar a descrição.
    private async Task<bool> HasPermissionToUpdateAsync(Ticket ticket, UpdateIntent intent, CancellationToken cancellationToken) =>
        await HasPermissionToEditOwnAsync(ticket, cancellationToken) ||
        await HasPermissionToManageAsync(cancellationToken) ||
        intent.Cancels && !intent.ChangesDescription && await HasPermissionToCancelAnyAsync(cancellationToken);

    private async Task<bool> HasPermissionToEditOwnAsync(Ticket ticket, CancellationToken cancellationToken) =>
        ticket.CreatedByUserId == userContext.UserId &&
        await permissionProvider.HasPermissionAsync(userContext.UserId, PermissionCodes.Tickets.UpdateOwn, cancellationToken);

    private Task<bool> HasPermissionToManageAsync(CancellationToken cancellationToken) =>
        permissionProvider.HasPermissionAsync(userContext.UserId, PermissionCodes.Tickets.Manage, cancellationToken);

    private Task<bool> HasPermissionToCancelAnyAsync(CancellationToken cancellationToken) =>
        permissionProvider.HasPermissionAsync(userContext.UserId, PermissionCodes.Tickets.Cancel, cancellationToken);

    private readonly record struct UpdateIntent(bool ChangesStatus, bool Cancels, bool ChangesDescription);
}
