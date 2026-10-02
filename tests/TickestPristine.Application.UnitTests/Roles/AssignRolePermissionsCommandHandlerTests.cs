using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Roles.AssignPermissions;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Roles;

public sealed class AssignRolePermissionsCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenRoleDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        var handler = new AssignRolePermissionsCommandHandler(context, userContext, permissionProvider);

        var command = new AssignRolePermissionsCommand
        {
            RoleId = Guid.NewGuid(),
            PermissionCodes = [PermissionCodes.Tickets.Create]
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.NotFound(command.RoleId));
    }

    [Fact]
    public async Task Handle_Should_ReplacePermissionsRaiseDomainEventAndInvalidateCache_WhenPermissionsAreValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        var role = new Role { Id = Guid.NewGuid(), Name = "Manager" };
        context.Roles.Add(role);
        context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = PermissionCodes.Tickets.DeleteOwn });

        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = firstUserId, RoleId = role.Id });
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = secondUserId, RoleId = role.Id });

        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        var handler = new AssignRolePermissionsCommandHandler(context, userContext, permissionProvider);

        var command = new AssignRolePermissionsCommand
        {
            RoleId = role.Id,
            PermissionCodes = [PermissionCodes.Tickets.Create, PermissionCodes.Tickets.ViewOwn]
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        List<string> permissionCodes = await context.RolePermissions
            .Where(p => p.RoleId == role.Id)
            .Select(p => p.PermissionCode)
            .ToListAsync();
        permissionCodes.ShouldBe([PermissionCodes.Tickets.Create, PermissionCodes.Tickets.ViewOwn], ignoreOrder: true);

        Role updated = await context.Roles.SingleAsync(r => r.Id == role.Id);
        updated.DomainEvents.ShouldContain(domainEvent => domainEvent is RolePermissionsAssignedDomainEvent);

        await permissionProvider.Received(1).InvalidateAsync(firstUserId, Arg.Any<CancellationToken>());
        await permissionProvider.Received(1).InvalidateAsync(secondUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnPrivilegeEscalation_WhenGrantingAdministrativePermissionCallerLacks()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        var role = new Role { Id = Guid.NewGuid(), Name = "Gestor" };
        context.Roles.Add(role);
        context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = PermissionCodes.Tickets.Create });
        await context.SaveChangesAsync();

        var callerId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(callerId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Roles.Manage]);

        var handler = new AssignRolePermissionsCommandHandler(context, userContext, permissionProvider);
        var command = new AssignRolePermissionsCommand
        {
            RoleId = role.Id,
            PermissionCodes = [PermissionCodes.Roles.Manage, PermissionCodes.Users.AssignRoles]
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.PrivilegeEscalation([PermissionCodes.GetDisplayName(PermissionCodes.Users.AssignRoles)]));

        List<string> permissionCodes = await context.RolePermissions
            .Where(p => p.RoleId == role.Id)
            .Select(p => p.PermissionCode)
            .ToListAsync();
        permissionCodes.ShouldBe([PermissionCodes.Tickets.Create]);
    }

    [Fact]
    public async Task Handle_Should_ReturnPrivilegeEscalation_WhenRemovingAdministrativePermissionCallerLacks()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        var gestorRole = new Role { Id = Guid.NewGuid(), Name = "Gestor" };
        context.Roles.Add(gestorRole);
        context.RolePermissions.Add(new RolePermission { RoleId = gestorRole.Id, PermissionCode = PermissionCodes.Roles.Manage });
        context.RolePermissions.Add(new RolePermission { RoleId = gestorRole.Id, PermissionCode = PermissionCodes.Users.AssignRoles });
        await context.SaveChangesAsync();

        var callerId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(callerId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Roles.Manage]);

        var handler = new AssignRolePermissionsCommandHandler(context, userContext, permissionProvider);
        var command = new AssignRolePermissionsCommand
        {
            RoleId = gestorRole.Id,
            PermissionCodes = [PermissionCodes.Roles.Manage]
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.PrivilegeEscalation([PermissionCodes.GetDisplayName(PermissionCodes.Users.AssignRoles)]));

        List<string> permissionCodes = await context.RolePermissions
            .Where(p => p.RoleId == gestorRole.Id)
            .Select(p => p.PermissionCode)
            .ToListAsync();
        permissionCodes.ShouldBe([PermissionCodes.Roles.Manage, PermissionCodes.Users.AssignRoles], ignoreOrder: true);
    }

    [Fact]
    public async Task Handle_Should_ReturnAdministratorRoleIsManaged_WhenRoleIsAdministrator()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        var adminRole = new Role { Id = Guid.NewGuid(), Name = "Administrador do Sistema", IsAdministrator = true };
        context.Roles.Add(adminRole);
        context.RolePermissions.Add(new RolePermission { RoleId = adminRole.Id, PermissionCode = PermissionCodes.Users.Read });
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([.. PermissionCodes.All]);

        var handler = new AssignRolePermissionsCommandHandler(context, userContext, permissionProvider);
        var command = new AssignRolePermissionsCommand { RoleId = adminRole.Id, PermissionCodes = [] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.AdministratorRoleIsManaged);
        (await context.RolePermissions.CountAsync(p => p.RoleId == adminRole.Id)).ShouldBe(1);
    }
}
