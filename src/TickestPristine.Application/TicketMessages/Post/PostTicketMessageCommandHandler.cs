using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
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
        var ticket = await context.Tickets
            .Where(t => t.Id == command.TicketId)
            .Select(t => new { t.CreatedByUserId, t.AssignedToUserId })
            .SingleOrDefaultAsync(cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<Guid>(TicketErrors.NotFound(command.TicketId));
        }

        bool isParticipant = ticket.CreatedByUserId == userContext.UserId || ticket.AssignedToUserId == userContext.UserId;

        if (!isParticipant)
        {
            bool canManageTickets = await permissionProvider.HasPermissionAsync(
                userContext.UserId,
                PermissionCodes.Tickets.Manage,
                cancellationToken);

            if (!canManageTickets)
            {
                return Result.Failure<Guid>(UserErrors.Unauthorized());
            }
        }

        var message = new TicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = command.TicketId,
            AuthorUserId = userContext.UserId,
            Content = command.Content,
            CreatedAtUtc = dateTimeProvider.UtcNow
        };

        message.Raise(new TicketMessagePostedDomainEvent(message.Id, message.TicketId));

        context.TicketMessages.Add(message);

        await context.SaveChangesAsync(cancellationToken);

        return message.Id;
    }
}
