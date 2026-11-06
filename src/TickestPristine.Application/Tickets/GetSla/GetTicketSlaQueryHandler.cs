using System.Data.Common;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetSla;

/// <summary>
/// Só o bloco <c>sla</c> do chamado, com o mesmo acesso do detalhe: participantes ou quem tem <c>tickets:manage</c>.
/// </summary>
internal sealed class GetTicketSlaQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IUserContext userContext,
    TicketViewAccess ticketViewAccess,
    TicketSlaReader ticketSlaReader,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTicketSlaQuery, TicketSlaResponse>
{
    public async Task<Result<TicketSlaResponse>> Handle(
        GetTicketSlaQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        Result access = await ticketViewAccess.EnsureCanViewAsync(connection, userContext.UserId, request.TicketId, cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<TicketSlaResponse>(access.Error);
        }

        return await ticketSlaReader.ReadAsync(connection, request.TicketId, dateTimeProvider.UtcNow, cancellationToken);
    }
}
