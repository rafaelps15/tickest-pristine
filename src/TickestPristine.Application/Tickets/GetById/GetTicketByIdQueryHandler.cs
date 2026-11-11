using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Permissions;
using TickestPristine.Application.Tickets.GetSla;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetById;

internal sealed class GetTicketByIdQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IUserContext userContext,
    TicketViewAccess ticketViewAccess,
    IPermissionProvider permissionProvider,
    TicketSlaReader ticketSlaReader,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTicketByIdQuery, TicketResponse>
{
    public async Task<Result<TicketResponse>> Handle(
        GetTicketByIdQuery request,
        CancellationToken cancellationToken)
    {
        bool canCancelAny = await permissionProvider.HasPermissionAsync(
            userContext.UserId, PermissionCodes.Tickets.Cancel, cancellationToken);
        bool canEditOwn = await permissionProvider.HasPermissionAsync(
            userContext.UserId, PermissionCodes.Tickets.UpdateOwn, cancellationToken);

        // O mesmo instante na situação do SLA e no bloco sla, para os dois concordarem.
        DateTime nowUtc = dateTimeProvider.UtcNow;

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        Result access = await ticketViewAccess.EnsureCanViewAsync(connection, userContext.UserId, request.TicketId, cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<TicketResponse>(access.Error);
        }

        const string sql =
            $"""
             SELECT
                 t.id AS {nameof(TicketResponse.Id)},
                 t.title AS {nameof(TicketResponse.Title)},
                 t.description AS {nameof(TicketResponse.Description)},
                 t.priority AS {nameof(TicketResponse.Priority)},
                 t.status AS {nameof(TicketResponse.Status)},
                 t.created_by_user_id AS {nameof(TicketResponse.OpenedByUserId)},
                 requester.first_name || ' ' || requester.last_name AS {nameof(TicketResponse.OpenedByName)},
                 t.assigned_to_user_id AS {nameof(TicketResponse.AssignedToUserId)},
                 assignee.first_name || ' ' || assignee.last_name AS {nameof(TicketResponse.AssignedToName)},
                 t.sector_id AS {nameof(TicketResponse.SectorId)},
                 s.name AS {nameof(TicketResponse.SectorName)},
                 t.created_at_utc AS {nameof(TicketResponse.CreatedAtUtc)},
                 t.due_at_utc AS {nameof(TicketResponse.DueAtUtc)},
                 t.sla_warning_at_utc AS {nameof(TicketResponse.SlaWarningAtUtc)},
                 t.resolved_at_utc AS {nameof(TicketResponse.ResolvedAtUtc)},
                 {TicketSql.SlaStatus} AS {nameof(TicketResponse.SlaStatus)},
                 {TicketSql.CanBeCanceled} AS {nameof(TicketResponse.CanCancel)},
                 {TicketSql.OwnerCancelDeadline} AS {nameof(TicketResponse.OwnerCancelDeadlineUtc)}
             FROM public.tickets t
             LEFT JOIN public.users requester ON requester.id = t.created_by_user_id
             LEFT JOIN public.users assignee ON assignee.id = t.assigned_to_user_id
             LEFT JOIN public.sectors s ON s.id = t.sector_id
             WHERE t.id = @TicketId AND t.deleted_at_utc IS NULL
             """;

        TicketResponse? ticket = await connection.QuerySingleOrDefaultAsync<TicketResponse>(
            new CommandDefinition(
                sql,
                new
                {
                    request.TicketId,
                    NowUtc = nowUtc,
                    CallerId = userContext.UserId,
                    CanCancelAny = canCancelAny,
                    CanEditOwn = canEditOwn,
                    Ticket.OwnerCancelWindowMinutes
                },
                cancellationToken: cancellationToken));

        if (ticket is null)
        {
            return Result.Failure<TicketResponse>(TicketErrors.NotFound(request.TicketId));
        }

        // O bloco sla não sai do SQL: o consumido e o restante seguem o calendário útil de cada cronômetro.
        Result<TicketSlaResponse> sla = await ticketSlaReader.ReadAsync(connection, request.TicketId, nowUtc, cancellationToken);

        if (sla.IsFailure)
        {
            return Result.Failure<TicketResponse>(sla.Error);
        }

        ticket.Sla = sla.Value;

        return ticket;
    }
}

