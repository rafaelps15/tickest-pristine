using System.Data.Common;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Tickets;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests.Abstractions;

public sealed class PaginationExtensionsTests(IntegrationTestWebAppFactory factory) : BaseQueryHandlerTest(factory)
{
    private const string DepartmentsPageSql =
        $"""
         SELECT COUNT(*)::int
         FROM departments;

         SELECT
             id AS {nameof(DepartmentRow.Id)},
             name AS {nameof(DepartmentRow.Name)}
         FROM departments
         ORDER BY name, id
         LIMIT @Limit OFFSET @Offset
         """;

    private const string MessagesCursorSql =
        $"""
         SELECT
             m.id AS {nameof(MessageRow.Id)},
             m.created_at_utc AS {nameof(MessageRow.CreatedAtUtc)}
         FROM ticket_messages m
         WHERE (@CursorDate::timestamptz IS NULL OR (m.created_at_utc, m.id) < (@CursorDate::timestamptz, @CursorId::uuid))
         ORDER BY m.created_at_utc DESC, m.id DESC
         LIMIT @Limit
         """;

    private static readonly DateTime CreatedAt = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task QueryPagedAsync_Should_ReturnRequestedPageAndNavigation_WhenPageIsInRange()
    {
        // Arrange
        await SeedDepartmentsAsync(5);

        // Act
        PagedResponse<DepartmentRow> result = await QueryDepartmentsAsync(new PageRequest(2, 2));

        // Assert
        result.Items.Select(d => d.Name).ShouldBe(["Departamento 3", "Departamento 4"]);
        result.Page.ShouldBe(2);
        result.PageSize.ShouldBe(2);
        result.TotalCount.ShouldBe(5);
        result.TotalPages.ShouldBe(3);
        result.HasPreviousPage.ShouldBeTrue();
        result.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryPagedAsync_Should_ReturnEmptyItemsWithTotals_WhenPageIsBeyondLastPage()
    {
        // Arrange
        await SeedDepartmentsAsync(3);

        // Act
        PagedResponse<DepartmentRow> result = await QueryDepartmentsAsync(new PageRequest(5, 2));

        // Assert
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(3);
        result.TotalPages.ShouldBe(2);
        result.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryPagedAsync_Should_UseDefaults_WhenPageAndPageSizeAreInvalid()
    {
        // Arrange
        await SeedDepartmentsAsync(1);

        // Act
        PagedResponse<DepartmentRow> result = await QueryDepartmentsAsync(new PageRequest(0, 0));

        // Assert
        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(PaginationExtensions.DefaultPageSize);
        result.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task QueryPagedAsync_Should_CapPageSize_WhenPageSizeExceedsMaximum()
    {
        // Act
        PagedResponse<DepartmentRow> result = await QueryDepartmentsAsync(new PageRequest(1, 1000));

        // Assert
        result.PageSize.ShouldBe(PaginationExtensions.MaxPageSize);
    }

    [Fact]
    public async Task QueryPagedAsync_Should_ReturnEmptyPage_WhenPageIsHugeEnoughToOverflowTheOffset()
    {
        // Arrange
        await SeedDepartmentsAsync(3);

        // Act
        PagedResponse<DepartmentRow> result = await QueryDepartmentsAsync(
            new PageRequest(int.MaxValue, PaginationExtensions.MaxPageSize));

        // Assert
        result.Items.ShouldBeEmpty();
        result.Page.ShouldBe(int.MaxValue);
        result.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task QueryPagedAsync_Should_ReturnEmptyPage_WhenThereAreNoItems()
    {
        // Act
        PagedResponse<DepartmentRow> result = await QueryDepartmentsAsync(new PageRequest(1, 20));

        // Assert
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
        result.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryCursorPagedAsync_Should_ReturnCursorOfLastItem_WhenThereIsANextPage()
    {
        // Arrange
        TicketMessage[] messages = await SeedMessagesAsync(3);

        // Act
        CursorPagedResponse<MessageRow> result = await QueryMessagesAsync(new CursorPageRequest(null, null, 2));

        // Assert
        result.Items.Select(m => m.Id).ShouldBe([messages[2].Id, messages[1].Id]);
        result.HasNextPage.ShouldBeTrue();
        result.NextCursorDate.ShouldBe(messages[1].CreatedAtUtc);
        result.NextCursorId.ShouldBe(messages[1].Id);
    }

    [Fact]
    public async Task QueryCursorPagedAsync_Should_ReturnNoCursor_WhenThereIsNoNextPage()
    {
        // Arrange
        await SeedMessagesAsync(2);

        // Act
        CursorPagedResponse<MessageRow> result = await QueryMessagesAsync(new CursorPageRequest(null, null, 2));

        // Assert
        result.Items.Count.ShouldBe(2);
        result.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryCursorPagedAsync_Should_UseDefaultPageSize_WhenPageSizeIsInvalid()
    {
        // Arrange
        await SeedMessagesAsync(PaginationExtensions.DefaultPageSize + 1);

        // Act
        CursorPagedResponse<MessageRow> result = await QueryMessagesAsync(new CursorPageRequest(null, null, 0));

        // Assert
        result.Items.Count.ShouldBe(PaginationExtensions.DefaultPageSize);
        result.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryCursorPagedAsync_Should_ReturnEmptyPageWithoutCursor_WhenThereAreNoItems()
    {
        // Act
        CursorPagedResponse<MessageRow> result = await QueryMessagesAsync(new CursorPageRequest(null, null, 20));

        // Assert
        result.Items.ShouldBeEmpty();
        result.NextCursorDate.ShouldBeNull();
        result.NextCursorId.ShouldBeNull();
        result.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryCursorPagedAsync_Should_OrderNewestFirstAndKeepOnlyItemsAfterCursor_WhenDatesTie()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        Guid[] tiedIds = [.. Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).Order()];
        TicketMessage older = QueryTicketTestData.CreateMessage(ticketId, Guid.NewGuid(), "older", CreatedAt.AddMinutes(-1));

        await using (ApplicationDbContext context = CreateDbContext())
        {
            context.TicketMessages.Add(older);
            foreach (Guid id in tiedIds)
            {
                context.TicketMessages.Add(QueryTicketTestData.CreateMessage(ticketId, Guid.NewGuid(), "tie", CreatedAt, id: id));
            }

            await context.SaveChangesAsync();
        }

        // Cursor no item empatado do meio: devem vir o empatado de id menor e depois o mais antigo.
        var paging = new CursorPageRequest(CreatedAt, tiedIds[1], null);

        // Act
        CursorPagedResponse<MessageRow> result = await QueryMessagesAsync(paging);

        // Assert
        result.Items.Select(m => m.Id).ShouldBe([tiedIds[0], older.Id]);
    }

    [Fact]
    public async Task QueryCursorPagedAsync_Should_ReturnEverythingNewestFirst_WhenThereIsNoCursor()
    {
        // Arrange
        TicketMessage newer = QueryTicketTestData.CreateMessage(Guid.NewGuid(), Guid.NewGuid(), "newer", CreatedAt);
        TicketMessage older = QueryTicketTestData.CreateMessage(Guid.NewGuid(), Guid.NewGuid(), "older", CreatedAt.AddMinutes(-1));

        await using (ApplicationDbContext context = CreateDbContext())
        {
            context.TicketMessages.AddRange(older, newer);
            await context.SaveChangesAsync();
        }

        // Act
        CursorPagedResponse<MessageRow> result = await QueryMessagesAsync(new CursorPageRequest(null, null, null));

        // Assert
        result.Items.Select(m => m.Id).ShouldBe([newer.Id, older.Id]);
    }

    private async Task<PagedResponse<DepartmentRow>> QueryDepartmentsAsync(PageRequest paging)
    {
        await using DbConnection connection = await DbConnectionFactory.OpenConnectionAsync();

        return await connection.QueryPagedAsync<DepartmentRow>(DepartmentsPageSql, null, paging);
    }

    private async Task<CursorPagedResponse<MessageRow>> QueryMessagesAsync(CursorPageRequest paging)
    {
        await using DbConnection connection = await DbConnectionFactory.OpenConnectionAsync();

        return await connection.QueryCursorPagedAsync<MessageRow>(
            MessagesCursorSql,
            null,
            paging,
            message => message.CreatedAtUtc,
            message => message.Id);
    }

    private async Task SeedDepartmentsAsync(int count)
    {
        await using ApplicationDbContext context = CreateDbContext();

        for (int i = 1; i <= count; i++)
        {
            context.Departments.Add(Department.Create($"Departamento {i}", "Descrição", responsibleUser: null));
        }

        await context.SaveChangesAsync();
    }

    // Mensagens de um minuto em um minuto, da mais antiga (índice 0) para a mais nova.
    private async Task<TicketMessage[]> SeedMessagesAsync(int count)
    {
        var ticketId = Guid.NewGuid();
        TicketMessage[] messages = [.. Enumerable.Range(0, count)
            .Select(i => QueryTicketTestData.CreateMessage(ticketId, Guid.NewGuid(), $"message {i}", CreatedAt.AddMinutes(i)))];

        await using ApplicationDbContext context = CreateDbContext();
        context.TicketMessages.AddRange(messages);
        await context.SaveChangesAsync();

        return messages;
    }

    public sealed class DepartmentRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; }
    }

    public sealed class MessageRow
    {
        public Guid Id { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }
}
