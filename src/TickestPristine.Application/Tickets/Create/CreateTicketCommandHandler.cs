using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Sla;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.Tickets.Create;

internal sealed class CreateTicketCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    ISlaCalendarProvider slaCalendarProvider,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateTicketCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTicketCommand command, CancellationToken cancellationToken)
    {
        Guid requesterId = command.RequesterId ?? userContext.UserId;

        // Abrir em nome de outra pessoa exige gerenciar chamados.
        if (requesterId != userContext.UserId &&
            !await permissionProvider.HasPermissionAsync(userContext.UserId, PermissionCodes.Tickets.Manage, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.Unauthorized());
        }

        // Solicitante e responsável são carregados numa única consulta, com o usuário logado, que já é válido mesmo
        // desativado. Usuário informado desativado conta como inexistente, como o setor inativo logo abaixo.
        Guid?[] referencedUserIds = [command.RequesterId, command.AssignedToUserId];
        Guid[] userIds = referencedUserIds.OfType<Guid>().Append(userContext.UserId).Distinct().ToArray();

        List<User> users = await context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id) && (u.IsActive || u.Id == userContext.UserId))
            .ToListAsync(cancellationToken);

        Guid? missingUserId = userIds.Where(id => users.All(u => u.Id != id)).Cast<Guid?>().FirstOrDefault();

        if (missingUserId is { } userId)
        {
            return Result.Failure<Guid>(UserErrors.NotFound(userId));
        }

        Sector? sector = await context.Sectors
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == command.SectorId && s.IsActive, cancellationToken);

        if (sector is null)
        {
            return Result.Failure<Guid>(SectorErrors.NotFound(command.SectorId));
        }

        // O plano fica gravado no chamado: mudar a política depois não altera os prazos deste chamado.
        var slaPlan = TicketSlaPlan.Default(command.Priority);

        Result<BusinessCalendar> calendar = await slaCalendarProvider.GetAsync(slaPlan.CalendarId, cancellationToken);

        if (calendar.IsFailure)
        {
            return Result.Failure<Guid>(calendar.Error);
        }

        DateTime createdAtUtc = dateTimeProvider.UtcNow;

        var ticket = Ticket.Create(
            command.Title,
            command.Description,
            command.Priority,
            sector,
            users.Single(u => u.Id == requesterId),
            users.SingleOrDefault(u => u.Id == command.AssignedToUserId),
            slaPlan,
            calendar.Value,
            userContext.UserId,
            createdAtUtc);

        (TicketHistory opening, IReadOnlyList<TicketSlaTimer> slaTimers) = ticket.RecordOpening(userContext.UserId);

        context.Tickets.Add(ticket);
        context.TicketSlaTimers.AddRange(slaTimers);
        context.TicketHistories.Add(opening);

        await context.SaveChangesAsync(cancellationToken);

        return ticket.Id;
    }
}
