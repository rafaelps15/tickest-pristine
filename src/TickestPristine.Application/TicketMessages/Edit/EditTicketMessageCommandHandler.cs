using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
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
            return Result.Failure(UserErrors.Unauthorized());
        }

        message.Content = command.Content;
        message.EditedAtUtc = dateTimeProvider.UtcNow;

        message.Raise(new TicketMessageEditedDomainEvent(message.Id, message.TicketId));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
