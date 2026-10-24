using TickestPristine.Application.Abstractions.Pagination;
using Microsoft.EntityFrameworkCore;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.UnitTests.Abstractions;

public sealed class PaginationExtensionsTests : BaseHandlerTest
{
    [Fact]
    public async Task ToPagedListAsync_Should_ReturnRequestedPageAndNavigation_WhenPageIsInRange()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        SeedDepartments(context, 5);
        await context.SaveChangesAsync();

        // Act
        PagedResponse<Department> result = await context.Departments
            .OrderBy(d => d.Name)
            .ToPagedListAsync(new PageRequest(2, 2));

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
    public async Task ToPagedListAsync_Should_ReturnEmptyItemsWithTotals_WhenPageIsBeyondLastPage()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        SeedDepartments(context, 3);
        await context.SaveChangesAsync();

        // Act
        PagedResponse<Department> result = await context.Departments
            .OrderBy(d => d.Name)
            .ToPagedListAsync(new PageRequest(5, 2));

        // Assert
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(3);
        result.TotalPages.ShouldBe(2);
        result.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public async Task ToPagedListAsync_Should_UseDefaults_WhenPageAndPageSizeAreInvalid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        SeedDepartments(context, 1);
        await context.SaveChangesAsync();

        // Act
        PagedResponse<Department> result = await context.Departments
            .OrderBy(d => d.Name)
            .ToPagedListAsync(new PageRequest(0, 0));

        // Assert
        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(PaginationExtensions.DefaultPageSize);
        result.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ToPagedListAsync_Should_CapPageSize_WhenPageSizeExceedsMaximum()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        // Act
        PagedResponse<Department> result = await context.Departments
            .OrderBy(d => d.Name)
            .ToPagedListAsync(new PageRequest(1, 1000));

        // Assert
        result.PageSize.ShouldBe(PaginationExtensions.MaxPageSize);
    }

    [Fact]
    public async Task ToCursorPagedListAsync_Should_ReturnCursorOfLastItem_WhenThereIsANextPage()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        SeedDepartments(context, 3);
        await context.SaveChangesAsync();

        // Act
        CursorPagedResponse<Department> result = await context.Departments
            .OrderBy(d => d.Name)
            .ToCursorPagedListAsync(new CursorPageRequest(null, null, 2), d => CreatedAt, d => d.Id);

        // Assert
        result.Items.Select(d => d.Name).ShouldBe(["Departamento 1", "Departamento 2"]);
        result.HasNextPage.ShouldBeTrue();
        result.NextCursorDate.ShouldBe(CreatedAt);
        result.NextCursorId.ShouldBe(result.Items[^1].Id);
    }

    [Fact]
    public async Task ToCursorPagedListAsync_Should_ReturnNoCursor_WhenThereIsNoNextPage()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        SeedDepartments(context, 2);
        await context.SaveChangesAsync();

        // Act
        CursorPagedResponse<Department> result = await context.Departments
            .OrderBy(d => d.Name)
            .ToCursorPagedListAsync(new CursorPageRequest(null, null, 2), d => CreatedAt, d => d.Id);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public async Task ToCursorPagedListAsync_Should_UseDefaultPageSize_WhenPageSizeIsInvalid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        SeedDepartments(context, PaginationExtensions.DefaultPageSize + 1);
        await context.SaveChangesAsync();

        // Act
        CursorPagedResponse<Department> result = await context.Departments
            .OrderBy(d => d.Name)
            .ToCursorPagedListAsync(new CursorPageRequest(null, null, 0), d => CreatedAt, d => d.Id);

        // Assert
        result.Items.Count.ShouldBe(PaginationExtensions.DefaultPageSize);
        result.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public async Task ToCursorPagedListAsync_Should_ReturnEmptyPageWithoutCursor_WhenThereAreNoItems()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        // Act
        CursorPagedResponse<Department> result = await context.Departments
            .OrderBy(d => d.Name)
            .ToCursorPagedListAsync(new CursorPageRequest(null, null, 20), d => CreatedAt, d => d.Id);

        // Assert
        result.Items.ShouldBeEmpty();
        result.NextCursorDate.ShouldBeNull();
        result.NextCursorId.ShouldBeNull();
        result.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public async Task ToPagedListAsync_Should_ReturnEmptyPage_WhenPageIsHugeEnoughToOverflowTheOffset()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        SeedDepartments(context, 3);
        await context.SaveChangesAsync();

        // Act
        PagedResponse<Department> result = await context.Departments
            .OrderBy(d => d.Name)
            .ToPagedListAsync(new PageRequest(int.MaxValue, PaginationExtensions.MaxPageSize));

        // Assert
        result.Items.ShouldBeEmpty();
        result.Page.ShouldBe(int.MaxValue);
        result.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task ToPagedListAsync_Should_ReturnEmptyPage_WhenThereAreNoItems()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        // Act
        PagedResponse<Department> result = await context.Departments
            .OrderBy(d => d.Name)
            .ToPagedListAsync(new PageRequest(1, 20));

        // Assert
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
        result.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public async Task AfterCursor_Should_OrderNewestFirstAndKeepOnlyItemsAfterCursor_WhenDatesTie()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var ticketId = Guid.NewGuid();
        Guid[] tiedIds = [.. Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).Order()];
        var older = new TicketMessage { Id = Guid.NewGuid(), TicketId = ticketId, AuthorUserId = Guid.NewGuid(), Content = "older", CreatedAtUtc = CreatedAt.AddMinutes(-1) };
        context.TicketMessages.Add(older);
        foreach (Guid id in tiedIds)
        {
            context.TicketMessages.Add(new TicketMessage { Id = id, TicketId = ticketId, AuthorUserId = Guid.NewGuid(), Content = "tie", CreatedAtUtc = CreatedAt });
        }
        await context.SaveChangesAsync();

        // Cursor no item empatado do meio: devem vir o empatado de id menor e depois o mais antigo.
        var paging = new CursorPageRequest(CreatedAt, tiedIds[1], null);

        // Act
        List<Guid> result = await context.TicketMessages
            .AfterCursor(paging, m => m.CreatedAtUtc, m => m.Id)
            .Select(m => m.Id)
            .ToListAsync();

        // Assert
        result.ShouldBe([tiedIds[0], older.Id]);
    }

    [Fact]
    public async Task AfterCursor_Should_ReturnEverythingNewestFirst_WhenThereIsNoCursor()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var newer = new TicketMessage { Id = Guid.NewGuid(), TicketId = Guid.NewGuid(), AuthorUserId = Guid.NewGuid(), Content = "newer", CreatedAtUtc = CreatedAt };
        var older = new TicketMessage { Id = Guid.NewGuid(), TicketId = Guid.NewGuid(), AuthorUserId = Guid.NewGuid(), Content = "older", CreatedAtUtc = CreatedAt.AddMinutes(-1) };
        context.TicketMessages.AddRange(older, newer);
        await context.SaveChangesAsync();

        // Act
        List<Guid> result = await context.TicketMessages
            .AfterCursor(new CursorPageRequest(null, null, null), m => m.CreatedAtUtc, m => m.Id)
            .Select(m => m.Id)
            .ToListAsync();

        // Assert
        result.ShouldBe([newer.Id, older.Id]);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void CursorPageRequest_Should_HaveCursor_OnlyWhenDateAndIdAreInformed(bool hasDate, bool hasId, bool expected)
    {
        // Arrange
        var paging = new CursorPageRequest(hasDate ? CreatedAt : null, hasId ? Guid.NewGuid() : null, null);

        // Act
        bool hasCursor = paging.HasCursor;

        // Assert
        hasCursor.ShouldBe(expected);
    }

    [Fact]
    public void CursorPageRequest_Should_MarkCursorDateAsUtc_WhenItArrivesUnspecified()
    {
        // Arrange
        var unspecified = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Unspecified);
        var paging = new CursorPageRequest(unspecified, Guid.NewGuid(), null);

        // Act
        DateTime cursorDate = paging.CursorDateUtc;

        // Assert
        cursorDate.Kind.ShouldBe(DateTimeKind.Utc);
        cursorDate.Ticks.ShouldBe(unspecified.Ticks);
    }

    private static readonly DateTime CreatedAt =new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    private static void SeedDepartments(TestDbContext context, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            context.Departments.Add(new Department
            {
                Id = Guid.NewGuid(),
                Name = $"Departamento {i}",
                Description = "Descrição",
                IsActive = true
            });
        }
    }
}
