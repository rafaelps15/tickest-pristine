using TickestPristine.Application.Departments.Deactivate;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Departments;
using Microsoft.EntityFrameworkCore;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Departments;

public sealed class DeactivateDepartmentCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenDepartmentDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new DeactivateDepartmentCommandHandler(context);
        var command = new DeactivateDepartmentCommand(Guid.NewGuid());

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DepartmentErrors.NotFound(command.DepartmentId));
    }

    [Fact]
    public async Task Handle_Should_DeactivateDepartment_WhenItExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var department = new Department { Id = Guid.NewGuid(), Name = "Support", Description = "Customer support department", IsActive = true };
        context.Departments.Add(department);
        await context.SaveChangesAsync();

        var handler = new DeactivateDepartmentCommandHandler(context);
        var command = new DeactivateDepartmentCommand(department.Id);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Department deactivated = await context.Departments.SingleAsync(d => d.Id == department.Id);
        deactivated.IsActive.ShouldBeFalse();
        deactivated.DomainEvents.ShouldContain(domainEvent => domainEvent is DepartmentDeactivatedDomainEvent);
    }

    [Theory]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.InProgress)]
    public async Task Handle_Should_ReturnConflict_WhenSectorHasActiveTickets(TicketStatus status)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var department = new Department { Id = Guid.NewGuid(), Name = "Support", Description = "Customer support department", IsActive = true };
        var sector = new Sector { Id = Guid.NewGuid(), Name = "Helpdesk", DepartmentId = department.Id, IsActive = true };
        context.Departments.Add(department);
        context.Sectors.Add(sector);
        context.Tickets.Add(CreateTicket(sector.Id, status));
        await context.SaveChangesAsync();

        var handler = new DeactivateDepartmentCommandHandler(context);

        // Act
        Result result = await handler.Handle(new DeactivateDepartmentCommand(department.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DepartmentErrors.HasActiveTickets());

        Department unchanged = await context.Departments.SingleAsync(d => d.Id == department.Id);
        unchanged.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_DeactivateSectors_WhenDeactivatingDepartment()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var department = new Department { Id = Guid.NewGuid(), Name = "Support", Description = "Customer support department", IsActive = true };
        var sector = new Sector { Id = Guid.NewGuid(), Name = "Helpdesk", DepartmentId = department.Id, IsActive = true };
        var otherDepartmentSector = new Sector { Id = Guid.NewGuid(), Name = "Payroll", DepartmentId = Guid.NewGuid(), IsActive = true };
        context.Departments.Add(department);
        context.Sectors.AddRange(sector, otherDepartmentSector);
        context.Tickets.Add(CreateTicket(sector.Id, TicketStatus.Closed));
        await context.SaveChangesAsync();

        var handler = new DeactivateDepartmentCommandHandler(context);

        // Act
        Result result = await handler.Handle(new DeactivateDepartmentCommand(department.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Sector deactivatedSector = await context.Sectors.SingleAsync(s => s.Id == sector.Id);
        deactivatedSector.IsActive.ShouldBeFalse();
        deactivatedSector.DomainEvents.ShouldContain(domainEvent => domainEvent is SectorDeactivatedDomainEvent);

        Sector untouchedSector = await context.Sectors.SingleAsync(s => s.Id == otherDepartmentSector.Id);
        untouchedSector.IsActive.ShouldBeTrue();
    }

    private static Ticket CreateTicket(Guid sectorId, TicketStatus status) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Printer is broken",
        Description = "The printer on the second floor is not working",
        Priority = TicketPriority.Medium,
        Status = status,
        CreatedByUserId = Guid.NewGuid(),
        SectorId = sectorId,
        CreatedAtUtc = DateTime.UtcNow
    };
}
