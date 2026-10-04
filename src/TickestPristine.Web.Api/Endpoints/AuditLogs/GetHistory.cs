using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.AuditLogs.GetHistory;
using TickestPristine.Application.Authorization;
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
            IQueryHandler<GetAuditHistoryQuery, List<AuditLogResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetAuditHistoryQuery(entityName, entityId);

            Result<List<AuditLogResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.AuditLogs)
        .HasPermission(PermissionCodes.Audit.Read);
    }
}
