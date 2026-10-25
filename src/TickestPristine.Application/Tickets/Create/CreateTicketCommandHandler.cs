using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Sla;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.SlaCalendars;
using TickestPristine.Application.TicketHistories;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

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
        Guid openedByUserId = command.RequesterId ?? userContext.UserId;

        // Abrir em nome de outra pessoa exige gerenciar chamados.
        Result access = await TicketAccessGuard.EnsureOwnerOrManagerAsync(
            permissionProvider,
            userContext.UserId,
            openedByUserId,
            ownPermission: null,
            cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<Guid>(access.Error);
        }

        // Solicitante e responsável informados são conferidos numa única consulta; o usuário logado já é válido. Usuário
        // desativado conta como inexistente, como o setor inativo logo abaixo.
        Guid[] referencedUserIds = new[] { command.RequesterId, command.AssignedToUserId }
            .OfType<Guid>()
            .Where(id => id != userContext.UserId)
            .Distinct()
            .ToArray();

        if (referencedUserIds.Length > 0)
        {
            List<Guid> existingUserIds = await context.Users
                .Where(u => referencedUserIds.Contains(u.Id) && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            Guid? missingUserId = referencedUserIds.Except(existingUserIds).Cast<Guid?>().FirstOrDefault();

            if (missingUserId is { } userId)
            {
                return Result.Failure<Guid>(UserErrors.NotFound(userId));
            }
        }

        bool sectorExists = await context.Sectors.AnyAsync(s => s.Id == command.SectorId && s.IsActive, cancellationToken);

        if (!sectorExists)
        {
            return Result.Failure<Guid>(SectorErrors.NotFound(command.SectorId));
        }

        // O plano fica gravado no chamado: mudar a política depois não altera os prazos deste chamado.
        TicketSlaPlan slaPlan = TicketSla.DefaultPlan(command.Priority);

        Result<BusinessCalendar> calendar = await slaCalendarProvider.GetAsync(slaPlan.CalendarId, cancellationToken);

        if (calendar.IsFailure)
        {
            return Result.Failure<Guid>(calendar.Error);
        }

        DateTime createdAtUtc = dateTimeProvider.UtcNow;
        var ticketId = Guid.NewGuid();

        List<TicketSlaTimer> slaTimers = TicketSlaClock.Start(ticketId, slaPlan, calendar.Value, createdAtUtc);

        // Os prazos do chamado seguem o primeiro ciclo de resolução, como o SLA que o resumo e os filtros já usam.
        TicketSlaTimer resolution = slaTimers.Single(t => t.Metric == SlaMetric.Resolution);

        var ticket = new Ticket
        {
            Id = ticketId,
            Title = command.Title,
            Description = command.Description,
            Priority = command.Priority,
            Status = TicketStatus.Open,
            CreatedByUserId = openedByUserId,
            AssignedToUserId = command.AssignedToUserId,
            SectorId = command.SectorId,
            CreatedAtUtc = createdAtUtc,
            DueAtUtc = resolution.DueOnUtc,
            SlaWarningAtUtc = resolution.WarningOnUtc,
            SlaPlan = slaPlan
        };

        ticket.Raise(new TicketCreatedDomainEvent(ticket.Id, userContext.UserId));

        context.Tickets.Add(ticket);
        context.TicketSlaTimers.AddRange(slaTimers);
        context.TicketHistories.Add(TicketHistoryEntries.Created(ticket.Id, userContext.UserId, createdAtUtc));

        await context.SaveChangesAsync(cancellationToken);

        return ticket.Id;
    }
}
