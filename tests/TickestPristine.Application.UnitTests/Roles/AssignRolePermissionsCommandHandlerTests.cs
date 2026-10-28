using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Roles.AssignPermissions;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Roles;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

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
            RoleName = "Gestor",
            PermissionCodes = [PermissionCodes.Tickets.Create]
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.NotFound(command.RoleName));
    }

    [Fact]
    public async Task Handle_Should_ReplacePermissionsRaiseDomainEventAndInvalidateCache_WhenPermissionsAreValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        Role role = CreateRole("Manager");
        context.Roles.Add(role);
        context.RolePermissions.Add(new RolePermission { RoleName = role.Name, PermissionCode = PermissionCodes.Tickets.ReopenOwn });

        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = firstUserId, RoleName = role.Name });
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = secondUserId, RoleName = role.Name });

        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        var handler = new AssignRolePermissionsCommandHandler(context, userContext, permissionProvider);

        var command = new AssignRolePermissionsCommand
        {
            RoleName = role.Name,
            PermissionCodes = [PermissionCodes.Tickets.Create, PermissionCodes.Tickets.ViewOwn]
        };

        using var requestCancellation = new CancellationTokenSource();

        // Act
        Result result = await handler.Handle(command, requestCancellation.Token);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        List<string> permissionCodes = await context.RolePermissions
            .Where(p => p.RoleName == role.Name)
            .Select(p => p.PermissionCode)
            .ToListAsync();
        permissionCodes.ShouldBe([PermissionCodes.Tickets.Create, PermissionCodes.Tickets.ViewOwn], ignoreOrder: true);
        role.DomainEvents.ShouldContain(e => e is RolePermissionsAssignedDomainEvent);

        await permissionProvider.Received(1).InvalidateAsync(firstUserId, CancellationToken.None);
        await permissionProvider.Received(1).InvalidateAsync(secondUserId, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_Should_ReturnPrivilegeEscalation_WhenGrantingAdministrativePermissionCallerLacks()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        Role role = CreateRole("Gestor");
        context.Roles.Add(role);
        context.RolePermissions.Add(new RolePermission { RoleName = role.Name, PermissionCode = PermissionCodes.Tickets.Create });
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
            RoleName = role.Name,
            PermissionCodes = [PermissionCodes.Roles.Manage, PermissionCodes.Users.AssignRoles]
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.PrivilegeEscalation([PermissionCatalog.GetDisplayName(PermissionCodes.Users.AssignRoles)]));

        List<string> permissionCodes = await context.RolePermissions
            .Where(p => p.RoleName == role.Name)
            .Select(p => p.PermissionCode)
            .ToListAsync();
        permissionCodes.ShouldBe([PermissionCodes.Tickets.Create]);
    }

    [Fact]
    public async Task Handle_Should_ReturnPrivilegeEscalation_WhenRemovingAdministrativePermissionCallerLacks()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        Role managerRole = CreateRole("Gestor");
        context.Roles.Add(managerRole);
        context.RolePermissions.Add(new RolePermission { RoleName = managerRole.Name, PermissionCode = PermissionCodes.Roles.Manage });
        context.RolePermissions.Add(new RolePermission { RoleName = managerRole.Name, PermissionCode = PermissionCodes.Users.AssignRoles });
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
            RoleName = managerRole.Name,
            PermissionCodes = [PermissionCodes.Roles.Manage]
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.PrivilegeEscalation([PermissionCatalog.GetDisplayName(PermissionCodes.Users.AssignRoles)]));

        List<string> permissionCodes = await context.RolePermissions
            .Where(p => p.RoleName == managerRole.Name)
            .Select(p => p.PermissionCode)
            .ToListAsync();
        permissionCodes.ShouldBe([PermissionCodes.Roles.Manage, PermissionCodes.Users.AssignRoles], ignoreOrder: true);
    }

    [Fact]
    public async Task Handle_Should_ReturnAdministratorRoleIsManaged_WhenRoleIsAdministrator()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        Role adminRole = CreateRole(RoleNames.Administrator);
        context.Roles.Add(adminRole);
        context.RolePermissions.Add(new RolePermission { RoleName = adminRole.Name, PermissionCode = PermissionCodes.Users.Read });
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([.. PermissionCatalog.All.Select(p => p.Code)]);

        var handler = new AssignRolePermissionsCommandHandler(context, userContext, permissionProvider);
        var command = new AssignRolePermissionsCommand { RoleName = adminRole.Name, PermissionCodes = [] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.AdministratorRoleIsManaged);
        (await context.RolePermissions.CountAsync(p => p.RoleName == adminRole.Name)).ShouldBe(1);
    }
}
