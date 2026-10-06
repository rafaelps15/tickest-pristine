using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;

namespace TickestPristine.Application.Tickets.GetByUser;

public sealed record GetTicketsByUserQuery(Guid UserId, PageRequest Paging) : IQuery<PagedResponse<TicketResponse>>;
