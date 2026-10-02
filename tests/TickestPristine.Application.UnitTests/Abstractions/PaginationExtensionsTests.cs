using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Domain.Departments;

namespace TickestPristine.Application.UnitTests.Abstractions;

public sealed class PaginationExtensionsTests : BaseHandlerTest
{
    [Fact]
    public async Task ToPagedResponseAsync_Should_ReturnRequestedPageAndNavigation_WhenPageIsInRange()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        SeedDepartments(context, 5);
        await context.SaveChangesAsync();

        // Act
        PagedResponse<Department> result = await context.Departments
            .OrderBy(d => d.Name)
            .ToPagedResponseAsync(page: 2, pageSize: 2);

        // Assert
        result.Items.Select(d => d.Name).ShouldBe(["Departamento 3", "Departamento 4"]);
        result.TotalCount.ShouldBe(5);
        result.TotalPages.ShouldBe(3);
        result.HasPreviousPage.ShouldBeTrue();
        result.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public async Task ToPagedResponseAsync_Should_UseDefaults_WhenPageAndPageSizeAreInvalid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        SeedDepartments(context, 1);
        await context.SaveChangesAsync();

        // Act
        PagedResponse<Department> result = await context.Departments
            .OrderBy(d => d.Name)
            .ToPagedResponseAsync(page: 0, pageSize: 0);

        // Assert
        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(PaginationExtensions.DefaultPageSize);
    }

    [Fact]
    public async Task ToPagedResponseAsync_Should_CapPageSize_WhenPageSizeExceedsMaximum()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        // Act
        PagedResponse<Department> result = await context.Departments
            .OrderBy(d => d.Name)
            .ToPagedResponseAsync(page: 1, pageSize: 1000);

        // Assert
        result.PageSize.ShouldBe(PaginationExtensions.MaxPageSize);
        result.TotalPages.ShouldBe(0);
        result.HasNextPage.ShouldBeFalse();
        result.HasPreviousPage.ShouldBeFalse();
    }

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
