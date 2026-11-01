using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketAttachments.Delete;

internal sealed class DeleteTicketAttachmentCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    ITicketActivityWriter activityWriter,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeleteTicketAttachmentCommand>
{
    public async Task<Result> Handle(DeleteTicketAttachmentCommand command, CancellationToken cancellationToken)
    {
        // O filtro global do chamado exclui os excluídos: mensagem ou anexo de chamado excluído não é mais alterado.
        TicketAttachment? attachment = await context.TicketAttachments
            .SingleOrDefaultAsync(
                a => a.Id == command.AttachmentId && context.Tickets.Any(t => t.Id == a.TicketId),
                cancellationToken);

        if (attachment is null)
        {
            return Result.Failure(TicketAttachmentErrors.NotFound(command.AttachmentId));
        }

        bool canDeleteAny = await permissionProvider.HasPermissionAsync(
            userContext.UserId,
            PermissionCodes.Tickets.Manage,
            cancellationToken);

        Result<TicketHistory> removal = attachment.Delete(userContext.UserId, canDeleteAny, dateTimeProvider.UtcNow);

        if (removal.IsFailure)
        {
            return removal;
        }

        // Sem rastreio: a última atividade é gravada depois do commit, sem a versão do chamado.
        Ticket ticket = await context.Tickets.AsNoTracking().SingleAsync(t => t.Id == attachment.TicketId, cancellationToken);

        context.TicketHistories.Add(removal.Value);

        await context.SaveChangesAsync(cancellationToken);

        ticket.RecordActivity(removal.Value);

        await activityWriter.WriteLastActivityAsync(ticket, cancellationToken);

        return Result.Success();
    }
}
