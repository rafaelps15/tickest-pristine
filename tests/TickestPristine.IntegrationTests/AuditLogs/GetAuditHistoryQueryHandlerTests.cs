using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.AuditLogs.GetHistory;
using TickestPristine.IntegrationTests.Abstractions;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Domain.Audit;
using NSubstitute;
using TickestPristine.SharedKernel;

namespace TickestPristine.IntegrationTests.AuditLogs;

public sealed class GetAuditHistoryQueryHandlerTests(IntegrationTestWebAppFactory factory) : BaseQueryHandlerTest(factory)
{
    private static readonly DateTime Now = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_ReturnEmptyList_WhenEntityHasNoHistory()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        var handler = new GetAuditHistoryQueryHandler(DbConnectionFactory);
        var query = new GetAuditHistoryQuery("Ticket", Guid.NewGuid().ToString(), new CursorPageRequest(null, null, 20));

        // Act
        Result<CursorPagedResponse<AuditLogResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_Should_ReturnOnlyEntityHistoryNewestFirst_WhenOtherEntitiesHaveHistory()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        string entityId = Guid.NewGuid().ToString();

        AuditLogEntry added = CreateEntry("Ticket", entityId, "Added", Now);
        AuditLogEntry modified = CreateEntry("Ticket", entityId, "Modified", Now.AddMinutes(5));
        AuditLogEntry otherEntity = CreateEntry("Ticket", Guid.NewGuid().ToString(), "Added", Now);
        AuditLogEntry otherType = CreateEntry("Sector", entityId, "Added", Now);
        context.AuditLogs.AddRange(added, modified, otherEntity, otherType);
        await context.SaveChangesAsync();

        var handler = new GetAuditHistoryQueryHandler(DbConnectionFactory);
        var query = new GetAuditHistoryQuery("Ticket", entityId, new CursorPageRequest(null, null, 20));

        // Act
        Result<CursorPagedResponse<AuditLogResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Select(a => a.Id).ShouldBe([modified.Id, added.Id]);
    }

    private static AuditLogEntry CreateEntry(string entityName, string entityId, string action, DateTime timestamp) => new()
    {
        Id = Guid.NewGuid(),
        EntityName = entityName,
        EntityId = entityId,
        Action = action,
        UserId = Guid.NewGuid(),
        Timestamp = timestamp,
        NewValues = "{}"
    };
}
