using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.AuditLogs.GetHistory;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Audit;

namespace TickestPristine.Application.UnitTests.AuditLogs;

public sealed class GetAuditHistoryQueryHandlerTests : BaseHandlerTest
{
    private static readonly DateTime Now = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_ReturnEmptyList_WhenEntityHasNoHistory()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new GetAuditHistoryQueryHandler(context);
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
        await using TestDbContext context = CreateDbContext();
        string entityId = Guid.NewGuid().ToString();

        AuditLogEntry added = CreateEntry("Ticket", entityId, "Added", Now);
        AuditLogEntry modified = CreateEntry("Ticket", entityId, "Modified", Now.AddMinutes(5));
        AuditLogEntry otherEntity = CreateEntry("Ticket", Guid.NewGuid().ToString(), "Added", Now);
        AuditLogEntry otherType = CreateEntry("Sector", entityId, "Added", Now);
        context.AuditLogs.AddRange(added, modified, otherEntity, otherType);
        await context.SaveChangesAsync();

        var handler = new GetAuditHistoryQueryHandler(context);
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
