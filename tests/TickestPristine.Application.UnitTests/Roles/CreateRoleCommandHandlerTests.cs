using Microsoft.EntityFrameworkCore;
using TickestPristine.Application.Roles.Create;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Roles;

namespace TickestPristine.Application.UnitTests.Roles;

public sealed class CreateRoleCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_CreateRoleWithTrimmedNameAndNoPermissions_WhenNameIsFree()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new CreateRoleCommandHandler(context);

        // Act
        Result<RoleResponse> result = await handler.Handle(new CreateRoleCommand { RoleName = "  Supervisor  " }, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Supervisor");
        result.Value.IsSystem.ShouldBeFalse();
        result.Value.IsDefault.ShouldBeFalse();
        result.Value.IsAdministrator.ShouldBeFalse();
        result.Value.ActiveUserCount.ShouldBe(0);
        result.Value.UserCount.ShouldBe(0);
        result.Value.PermissionCodes.ShouldBeEmpty();

        Role saved = await context.Roles.SingleAsync(r => r.Id == result.Value.Id);
        saved.Name.ShouldBe("Supervisor");
        saved.DomainEvents.ShouldContain(domainEvent => domainEvent is RoleCreatedDomainEvent);
        (await context.RolePermissions.AnyAsync(p => p.RoleId == saved.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnNameNotUnique_WhenRoleWithTheNameExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        context.Roles.Add(CreateRole("Supervisor"));
        await context.SaveChangesAsync();

        var handler = new CreateRoleCommandHandler(context);

        // Act
        Result<RoleResponse> result = await handler.Handle(new CreateRoleCommand { RoleName = " Supervisor " }, CancellationToken.None);

        // Assert: a comparação sem maiúsculas é do citext no PostgreSQL, coberta no teste de integração
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.NameNotUnique("Supervisor"));
        (await context.Roles.CountAsync()).ShouldBe(1);
    }
}
