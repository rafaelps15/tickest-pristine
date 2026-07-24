using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketMessages.Edit;

internal sealed class EditTicketMessageCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<EditTicketMessageCommand>
{
    public async Task<Result> Handle(EditTicketMessageCommand command, CancellationToken cancellationToken)
    {
        TicketMessage? message = await context.TicketMessages
            .SingleOrDefaultAsync(m => m.Id == command.MessageId, cancellationToken);

        if (message is null)
        {
            return Result.Failure(TicketMessageErrors.NotFound(command.MessageId));
        }

        if (message.AuthorUserId != userContext.UserId)
        {
            return Result.Failure(TicketMessageErrors.Unauthorized());
        }

        message.Edit(command.Content, dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
