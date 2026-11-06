using TickestPristine.Application.Abstractions.Pagination;

namespace TickestPristine.Application.UnitTests.Abstractions;

public sealed class PaginationExtensionsTests
{
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

    [Theory]
    [InlineData(null, null, 1, PaginationExtensions.DefaultPageSize)]
    [InlineData(0, 0, 1, PaginationExtensions.DefaultPageSize)]
    [InlineData(-3, -5, 1, PaginationExtensions.DefaultPageSize)]
    [InlineData(3, 1000, 3, PaginationExtensions.MaxPageSize)]
    [InlineData(2, 10, 2, 10)]
    public void PageRequest_Should_NormalizePageAndSize_WhenValuesAreMissingOrOutOfRange(
        int? page,
        int? pageSize,
        int expectedPage,
        int expectedSize)
    {
        // Arrange
        var paging = new PageRequest(page, pageSize);

        // Act
        (int pageNumber, int size) = (paging.PageNumber, paging.Size);

        // Assert
        pageNumber.ShouldBe(expectedPage);
        size.ShouldBe(expectedSize);
    }

    [Fact]
    public void PageRequest_Should_ComputeOffsetWithoutOverflow_WhenPageIsHuge()
    {
        // Arrange
        var paging = new PageRequest(int.MaxValue, PaginationExtensions.MaxPageSize);

        // Act
        long offset = paging.Offset;

        // Assert
        offset.ShouldBe((int.MaxValue - 1L) * PaginationExtensions.MaxPageSize);
    }

    [Theory]
    [InlineData(null, PaginationExtensions.DefaultPageSize)]
    [InlineData(0, PaginationExtensions.DefaultPageSize)]
    [InlineData(1000, PaginationExtensions.MaxPageSize)]
    [InlineData(5, 5)]
    public void CursorPageRequest_Should_NormalizePageSize_WhenItIsMissingOrOutOfRange(int? pageSize, int expectedSize)
    {
        // Arrange
        var paging = new CursorPageRequest(null, null, pageSize);

        // Act
        int size = paging.Size;

        // Assert
        size.ShouldBe(expectedSize);
    }

    private static readonly DateTime CreatedAt = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
}
