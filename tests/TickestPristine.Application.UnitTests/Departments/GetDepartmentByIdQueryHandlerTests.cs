using TickestPristine.Application.Departments.GetById;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Departments;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Departments;

public sealed class GetDepartmentByIdQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenDepartmentDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new GetDepartmentByIdQueryHandler(context);
        var query = new GetDepartmentByIdQuery(Guid.NewGuid());

        // Act
        Result<DepartmentResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Departments.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnDepartment_WhenItExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var department = Department.Create("Support", "Customer support department");
        context.Departments.Add(department);
        await context.SaveChangesAsync();

        var handler = new GetDepartmentByIdQueryHandler(context);
        var query = new GetDepartmentByIdQuery(department.Id);

        // Act
        Result<DepartmentResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Support");
        result.Value.IsActive.ShouldBeTrue();
    }
}
