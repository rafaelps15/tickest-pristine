using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.AuditLogs.GetHistory;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.AuditLogs;

internal sealed class GetHistory : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // entityId é texto porque chaves compostas chegam com as partes separadas por vírgula.
        app.MapGet("audit-logs/{entityName}/{entityId}", async (
            string entityName,
            string entityId,
            [AsParameters] CursorPageRequest paging,
            IQueryHandler<GetAuditHistoryQuery, CursorPagedResponse<AuditLogResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetAuditHistoryQuery(entityName, entityId, paging);

            Result<CursorPagedResponse<AuditLogResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.AuditRead)
        .WithTags(Tags.AuditLogs);
    }
}
