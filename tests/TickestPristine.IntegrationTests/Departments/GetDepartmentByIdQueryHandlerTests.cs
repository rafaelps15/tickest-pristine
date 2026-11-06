using TickestPristine.Application.Departments.GetById;
using TickestPristine.IntegrationTests.Abstractions;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Domain.Departments;
using NSubstitute;
using TickestPristine.SharedKernel;

namespace TickestPristine.IntegrationTests.Departments;

public sealed class GetDepartmentByIdQueryHandlerTests(IntegrationTestWebAppFactory factory) : BaseQueryHandlerTest(factory)
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenDepartmentDoesNotExist()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        var handler = new GetDepartmentByIdQueryHandler(DbConnectionFactory);
        var query = new GetDepartmentByIdQuery(Guid.NewGuid());

        // Act
        Result<DepartmentResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DepartmentErrors.NotFound(query.DepartmentId));
    }

    [Fact]
    public async Task Handle_Should_ReturnDepartment_WhenItExists()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        var department = Department.Create("Support", "Customer support department", responsibleUser: null);
        context.Departments.Add(department);
        await context.SaveChangesAsync();

        var handler = new GetDepartmentByIdQueryHandler(DbConnectionFactory);
        var query = new GetDepartmentByIdQuery(department.Id);

        // Act
        Result<DepartmentResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Support");
        result.Value.IsActive.ShouldBeTrue();
    }
}
