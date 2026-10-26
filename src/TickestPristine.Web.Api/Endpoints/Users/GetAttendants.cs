using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Users.GetAttendants;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Users;

internal sealed class GetAttendants : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Exige só usuário ativo: o handler confere users:read, para responder 403 com mensagem de negócio. O limite de
        // requisições é o global, por usuário.
        app.MapGet("users/attendants", async (
            string? searchTerm,
            Guid? sectorId,
            Guid? prioritySectorId,
            [AsParameters] PageRequest paging,
            IQueryHandler<GetBacklogAttendantsQuery, PagedResponse<AttendantResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetBacklogAttendantsQuery(searchTerm, sectorId, prioritySectorId, paging);

            Result<PagedResponse<AttendantResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .RequireAuthorization();
    }
}
