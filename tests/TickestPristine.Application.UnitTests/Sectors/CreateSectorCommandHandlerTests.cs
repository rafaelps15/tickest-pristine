using TickestPristine.Application.Sectors.Create;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Sectors;

public sealed class CreateSectorCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenDepartmentDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new CreateSectorCommandHandler(context);
        var command = new CreateSectorCommand { Name = "Helpdesk", DepartmentId = Guid.NewGuid() };

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DepartmentErrors.NotFound(command.DepartmentId));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenDepartmentIsInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var department = new Department { Id = Guid.NewGuid(), Name = "Support", Description = "Customer support department", IsActive = false };
        context.Departments.Add(department);
        await context.SaveChangesAsync();

        var handler = new CreateSectorCommandHandler(context);
        var command = new CreateSectorCommand { Name = "Helpdesk", DepartmentId = department.Id };

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DepartmentErrors.NotFound(command.DepartmentId));
    }

    [Fact]
    public async Task Handle_Should_CreateSector_WhenDepartmentExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var department = new Department { Id = Guid.NewGuid(), Name = "Support", Description = "Customer support department", IsActive = true };
        context.Departments.Add(department);
        await context.SaveChangesAsync();

        var handler = new CreateSectorCommandHandler(context);
        var command = new CreateSectorCommand { Name = "Helpdesk", DepartmentId = department.Id };

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Sector sector = await context.Sectors.SingleAsync(s => s.Id == result.Value);
        sector.Name.ShouldBe("Helpdesk");
        sector.DepartmentId.ShouldBe(department.Id);
        sector.IsActive.ShouldBeTrue();
        sector.DomainEvents.ShouldContain(domainEvent => domainEvent is SectorCreatedDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenDepartmentHasActiveSectorWithSameName()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var department = new Department { Id = Guid.NewGuid(), Name = "Support", Description = "Customer support department", IsActive = true };
        context.Departments.Add(department);
        context.Sectors.Add(new Sector { Id = Guid.NewGuid(), Name = "Helpdesk", DepartmentId = department.Id, IsActive = true });
        await context.SaveChangesAsync();

        var handler = new CreateSectorCommandHandler(context);
        var command = new CreateSectorCommand { Name = "Helpdesk", DepartmentId = department.Id };

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SectorErrors.NameNotUnique(command.Name));
    }

    [Fact]
    public async Task Handle_Should_CreateSector_WhenSameNameExistsInAnotherDepartment()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var department = new Department { Id = Guid.NewGuid(), Name = "Support", Description = "Customer support department", IsActive = true };
        context.Departments.Add(department);
        context.Sectors.Add(new Sector { Id = Guid.NewGuid(), Name = "Helpdesk", DepartmentId = Guid.NewGuid(), IsActive = true });
        await context.SaveChangesAsync();

        var handler = new CreateSectorCommandHandler(context);
        var command = new CreateSectorCommand { Name = "Helpdesk", DepartmentId = department.Id };

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }
}
