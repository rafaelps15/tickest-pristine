using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Users.AssignRoles;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class AssignUserRolesCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        var handler = new AssignUserRolesCommandHandler(context, userContext, permissionProvider);

        var command = new AssignUserRolesCommand { UserId = Guid.NewGuid(), RoleIds = [Guid.NewGuid()] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(command.UserId));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenARoleDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "Test",
            LastName = "User",
            Code = $"usr_{Ulid.NewUlid()}"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        var handler = new AssignUserRolesCommandHandler(context, userContext, permissionProvider);

        var command = new AssignUserRolesCommand { UserId = user.Id, RoleIds = [Guid.NewGuid()] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.NotFound(command.RoleIds[0]));
    }

    [Fact]
    public async Task Handle_Should_ReplaceRolesRaiseDomainEventAndInvalidateCache_WhenRolesAreValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "Test",
            LastName = "User",
            Code = $"usr_{Ulid.NewUlid()}"
        };
        context.Users.Add(user);

        var oldRole = new Role { Id = Guid.NewGuid(), Name = "Requester" };
        var newRole = new Role { Id = Guid.NewGuid(), Name = "Manager" };
        context.Roles.Add(oldRole);
        context.Roles.Add(newRole);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = user.Id, RoleId = oldRole.Id });

        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        var handler = new AssignUserRolesCommandHandler(context, userContext, permissionProvider);

        var command = new AssignUserRolesCommand { UserId = user.Id, RoleIds = [newRole.Id] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        List<Guid> assignedRoleIds = await context.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.RoleId)
            .ToListAsync();
        assignedRoleIds.ShouldBe([newRole.Id]);

        User updated = await context.Users.SingleAsync(u => u.Id == user.Id);
        updated.DomainEvents.ShouldContain(domainEvent => domainEvent is UserRolesAssignedDomainEvent);

        await permissionProvider.Received(1).InvalidateAsync(user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnPrivilegeEscalation_WhenRoleHasAdministrativePermissionCallerLacks()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "Test",
            LastName = "User",
            Code = $"usr_{Ulid.NewUlid()}"
        };
        context.Users.Add(user);

        var gestorRole = new Role { Id = Guid.NewGuid(), Name = "Gestor" };
        context.Roles.Add(gestorRole);
        context.RolePermissions.Add(new RolePermission { Id = Guid.NewGuid(), RoleId = gestorRole.Id, PermissionCode = PermissionCodes.Users.Read });
        context.RolePermissions.Add(new RolePermission { Id = Guid.NewGuid(), RoleId = gestorRole.Id, PermissionCode = PermissionCodes.Roles.Manage });

        await context.SaveChangesAsync();

        var callerId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(callerId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Users.Read, PermissionCodes.Users.AssignRoles]);

        var handler = new AssignUserRolesCommandHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand { UserId = user.Id, RoleIds = [gestorRole.Id] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.PrivilegeEscalation([PermissionCodes.GetDisplayName(PermissionCodes.Roles.Manage)]));

        (await context.UserRoles.AnyAsync(ur => ur.UserId == user.Id)).ShouldBeFalse();
        await permissionProvider.DidNotReceive().InvalidateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_AllowRoleWithOnlyTicketPermissions_WhenCallerLacksThem()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "Test",
            LastName = "User",
            Code = $"usr_{Ulid.NewUlid()}"
        };
        context.Users.Add(user);

        var agentRole = new Role { Id = Guid.NewGuid(), Name = "Agente" };
        context.Roles.Add(agentRole);
        context.RolePermissions.Add(new RolePermission { Id = Guid.NewGuid(), RoleId = agentRole.Id, PermissionCode = PermissionCodes.Tickets.Manage });

        await context.SaveChangesAsync();

        var callerId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(callerId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Users.AssignRoles]);

        var handler = new AssignUserRolesCommandHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand { UserId = user.Id, RoleIds = [agentRole.Id] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnPrivilegeEscalation_WhenRemovingRoleWithAdministrativePermissionCallerLacks()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.com",
            FirstName = "Admin",
            LastName = "User",
            Code = $"usr_{Ulid.NewUlid()}"
        };
        context.Users.Add(admin);

        var adminRole = new Role { Id = Guid.NewGuid(), Name = "Admin" };
        context.Roles.Add(adminRole);
        context.RolePermissions.Add(new RolePermission { Id = Guid.NewGuid(), RoleId = adminRole.Id, PermissionCode = PermissionCodes.Roles.Manage });
        context.RolePermissions.Add(new RolePermission { Id = Guid.NewGuid(), RoleId = adminRole.Id, PermissionCode = PermissionCodes.Users.AssignRoles });
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = admin.Id, RoleId = adminRole.Id });

        await context.SaveChangesAsync();

        var callerId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(callerId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Users.AssignRoles]);

        var handler = new AssignUserRolesCommandHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand { UserId = admin.Id, RoleIds = [] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.PrivilegeEscalation([PermissionCodes.GetDisplayName(PermissionCodes.Roles.Manage)]));

        List<Guid> assignedRoleIds = await context.UserRoles
            .Where(ur => ur.UserId == admin.Id)
            .Select(ur => ur.RoleId)
            .ToListAsync();
        assignedRoleIds.ShouldBe([adminRole.Id]);
    }

    [Fact]
    public async Task Handle_Should_IgnoreRolesUserAlreadyHas_WhenCheckingPrivilegeEscalation()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "Test",
            LastName = "User",
            Code = $"usr_{Ulid.NewUlid()}"
        };
        context.Users.Add(user);

        var gestorRole = new Role { Id = Guid.NewGuid(), Name = "Gestor" };
        var agentRole = new Role { Id = Guid.NewGuid(), Name = "Agente" };
        context.Roles.Add(gestorRole);
        context.Roles.Add(agentRole);
        context.RolePermissions.Add(new RolePermission { Id = Guid.NewGuid(), RoleId = gestorRole.Id, PermissionCode = PermissionCodes.Roles.Manage });
        context.RolePermissions.Add(new RolePermission { Id = Guid.NewGuid(), RoleId = agentRole.Id, PermissionCode = PermissionCodes.Tickets.Manage });
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = user.Id, RoleId = gestorRole.Id });

        await context.SaveChangesAsync();

        var callerId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(callerId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Users.AssignRoles]);

        var handler = new AssignUserRolesCommandHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand { UserId = user.Id, RoleIds = [gestorRole.Id, agentRole.Id] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnLastAdministrator_WhenRemovingAdministratorRoleFromOnlyAdministrator()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.com",
            FirstName = "Admin",
            LastName = "User",
            Code = $"usr_{Ulid.NewUlid()}"
        };
        context.Users.Add(admin);

        var adminRole = new Role { Id = Guid.NewGuid(), Name = "Admin", IsAdministrator = true };
        context.Roles.Add(adminRole);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = admin.Id, RoleId = adminRole.Id });
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(admin.Id);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(admin.Id, Arg.Any<CancellationToken>())
            .Returns([.. PermissionCodes.All]);

        var handler = new AssignUserRolesCommandHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand { UserId = admin.Id, RoleIds = [] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.LastAdministrator);
        (await context.UserRoles.AnyAsync(ur => ur.UserId == admin.Id && ur.RoleId == adminRole.Id)).ShouldBeTrue();
        (await context.Roles.SingleAsync(r => r.Id == adminRole.Id)).Version.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_RemoveAdministratorRoleAndIncrementVersion_WhenAnotherAdministratorExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.com",
            FirstName = "Admin",
            LastName = "User",
            Code = $"usr_{Ulid.NewUlid()}"
        };
        var otherAdmin = new User
        {
            Id = Guid.NewGuid(),
            Email = "other@example.com",
            FirstName = "Other",
            LastName = "Admin",
            Code = $"usr_{Ulid.NewUlid()}"
        };
        context.Users.AddRange(admin, otherAdmin);

        var adminRole = new Role { Id = Guid.NewGuid(), Name = "Admin", IsAdministrator = true };
        context.Roles.Add(adminRole);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = admin.Id, RoleId = adminRole.Id });
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = otherAdmin.Id, RoleId = adminRole.Id });
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(otherAdmin.Id);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(otherAdmin.Id, Arg.Any<CancellationToken>())
            .Returns([.. PermissionCodes.All]);

        var handler = new AssignUserRolesCommandHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand { UserId = admin.Id, RoleIds = [] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.UserRoles.AnyAsync(ur => ur.UserId == admin.Id)).ShouldBeFalse();

        Role administratorRole = await context.Roles.SingleAsync(r => r.Id == adminRole.Id);
        administratorRole.Version.ShouldBe(1);
    }
}
