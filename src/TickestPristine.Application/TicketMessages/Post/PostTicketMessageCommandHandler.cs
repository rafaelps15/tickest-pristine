using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketMessages.Post;

internal sealed class PostTicketMessageCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<PostTicketMessageCommand, Guid>
{
    public async Task<Result<Guid>> Handle(PostTicketMessageCommand command, CancellationToken cancellationToken)
    {
        Result access = await TicketAccessGuard.EnsureCanViewAsync(
            context,
            permissionProvider,
            userContext.UserId,
            command.TicketId,
            cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<Guid>(access.Error);
        }

        var message = new TicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = command.TicketId,
            AuthorUserId = userContext.UserId,
            Content = command.Content,
            CreatedAtUtc = dateTimeProvider.UtcNow
        };

        message.Raise(new TicketMessagePostedDomainEvent(message.Id, message.TicketId, message.AuthorUserId));

        context.TicketMessages.Add(message);

        context.TicketHistories.Add(TicketHistoryEntries.Create(
            message.TicketId,
            message.AuthorUserId,
            TicketHistoryAction.MessageAdded,
            TicketHistoryDescriptions.MessagePosted,
            dateTimeProvider.UtcNow));

        await context.SaveChangesAsync(cancellationToken);

        return message.Id;
    }
}
