using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
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

        var command = new AssignUserRolesCommand { UserId = Guid.NewGuid(), RoleNames = ["Gestor"] };

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
            Code = UserCodes.New()
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        var handler = new AssignUserRolesCommandHandler(context, userContext, permissionProvider);

        var command = new AssignUserRolesCommand { UserId = user.Id, RoleNames = ["Gestor"] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.NotFound(command.RoleNames[0]));
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
            Code = UserCodes.New()
        };
        context.Users.Add(user);

        Role oldRole = CreateRole("Colaborador");
        Role newRole = CreateRole("Manager");
        context.Roles.Add(oldRole);
        context.Roles.Add(newRole);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = user.Id, RoleName = oldRole.Name });

        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        var handler = new AssignUserRolesCommandHandler(context, userContext, permissionProvider);

        var command = new AssignUserRolesCommand { UserId = user.Id, RoleNames = [newRole.Name] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        List<string> assignedRoleNames = await context.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.RoleName)
            .ToListAsync();
        assignedRoleNames.ShouldBe([newRole.Name]);

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
            Code = UserCodes.New()
        };
        context.Users.Add(user);

        Role gestorRole = CreateRole("Gestor");
        context.Roles.Add(gestorRole);
        context.RolePermissions.Add(new RolePermission { RoleName = gestorRole.Name, PermissionCode = PermissionCodes.Users.Read });
        context.RolePermissions.Add(new RolePermission { RoleName = gestorRole.Name, PermissionCode = PermissionCodes.Roles.Manage });

        await context.SaveChangesAsync();

        var callerId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(callerId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Users.Read, PermissionCodes.Users.AssignRoles]);

        var handler = new AssignUserRolesCommandHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand { UserId = user.Id, RoleNames = [gestorRole.Name] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.PrivilegeEscalation([Permission.GetDisplayName(PermissionCodes.Roles.Manage)]));

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
            Code = UserCodes.New()
        };
        context.Users.Add(user);

        Role agentRole = CreateRole("Agente");
        context.Roles.Add(agentRole);
        context.RolePermissions.Add(new RolePermission { RoleName = agentRole.Name, PermissionCode = PermissionCodes.Tickets.Manage });

        await context.SaveChangesAsync();

        var callerId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(callerId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Users.AssignRoles]);

        var handler = new AssignUserRolesCommandHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand { UserId = user.Id, RoleNames = [agentRole.Name] };

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
            FirstName = "Administrador",
            LastName = "User",
            Code = UserCodes.New()
        };
        context.Users.Add(admin);

        Role adminRole = CreateRole("Administrador");
        context.Roles.Add(adminRole);
        context.RolePermissions.Add(new RolePermission { RoleName = adminRole.Name, PermissionCode = PermissionCodes.Roles.Manage });
        context.RolePermissions.Add(new RolePermission { RoleName = adminRole.Name, PermissionCode = PermissionCodes.Users.AssignRoles });
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = admin.Id, RoleName = adminRole.Name });

        await context.SaveChangesAsync();

        var callerId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(callerId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Users.AssignRoles]);

        var handler = new AssignUserRolesCommandHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand { UserId = admin.Id, RoleNames = [] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.PrivilegeEscalation([Permission.GetDisplayName(PermissionCodes.Roles.Manage)]));

        List<string> assignedRoleNames = await context.UserRoles
            .Where(ur => ur.UserId == admin.Id)
            .Select(ur => ur.RoleName)
            .ToListAsync();
        assignedRoleNames.ShouldBe([adminRole.Name]);
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
            Code = UserCodes.New()
        };
        context.Users.Add(user);

        Role gestorRole = CreateRole("Gestor");
        Role agentRole = CreateRole("Agente");
        context.Roles.Add(gestorRole);
        context.Roles.Add(agentRole);
        context.RolePermissions.Add(new RolePermission { RoleName = gestorRole.Name, PermissionCode = PermissionCodes.Roles.Manage });
        context.RolePermissions.Add(new RolePermission { RoleName = agentRole.Name, PermissionCode = PermissionCodes.Tickets.Manage });
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = user.Id, RoleName = gestorRole.Name });

        await context.SaveChangesAsync();

        var callerId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(callerId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Users.AssignRoles]);

        var handler = new AssignUserRolesCommandHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand { UserId = user.Id, RoleNames = [gestorRole.Name, agentRole.Name] };

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
            FirstName = "Administrador",
            LastName = "User",
            Code = UserCodes.New()
        };
        context.Users.Add(admin);

        Role adminRole = CreateRole(Role.Administrator.Name);
        context.Roles.Add(adminRole);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = admin.Id, RoleName = adminRole.Name });
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(admin.Id);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(admin.Id, Arg.Any<CancellationToken>())
            .Returns([.. Permission.All.Select(p => p.Code)]);

        var handler = new AssignUserRolesCommandHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand { UserId = admin.Id, RoleNames = [] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.LastAdministrator);
        (await context.UserRoles.AnyAsync(ur => ur.UserId == admin.Id && ur.RoleName == adminRole.Name)).ShouldBeTrue();
        context.Entry(await context.Roles.SingleAsync(r => r.Name == adminRole.Name)).Property<int>("Version").CurrentValue.ShouldBe(0);
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
            FirstName = "Administrador",
            LastName = "User",
            Code = UserCodes.New()
        };
        var otherAdmin = new User
        {
            Id = Guid.NewGuid(),
            Email = "other@example.com",
            FirstName = "Other",
            LastName = "Administrador",
            Code = UserCodes.New(),
            IsActive = true
        };
        context.Users.AddRange(admin, otherAdmin);

        Role adminRole = CreateRole(Role.Administrator.Name);
        context.Roles.Add(adminRole);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = admin.Id, RoleName = adminRole.Name });
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = otherAdmin.Id, RoleName = adminRole.Name });
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(otherAdmin.Id);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(otherAdmin.Id, Arg.Any<CancellationToken>())
            .Returns([.. Permission.All.Select(p => p.Code)]);

        var handler = new AssignUserRolesCommandHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand { UserId = admin.Id, RoleNames = [] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.UserRoles.AnyAsync(ur => ur.UserId == admin.Id)).ShouldBeFalse();

        Role administratorRole = await context.Roles.SingleAsync(r => r.Name == adminRole.Name);
        context.Entry(administratorRole).Property<int>("Version").CurrentValue.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_ReturnLastAdministrator_WhenOtherAdministratorIsDeactivated()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User admin = CreateUser(isActive: true);
        User deactivatedAdmin = CreateUser(isActive: false);
        context.Users.AddRange(admin, deactivatedAdmin);

        Role adminRole = CreateRole(Role.Administrator.Name);
        context.Roles.Add(adminRole);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = admin.Id, RoleName = adminRole.Name });
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = deactivatedAdmin.Id, RoleName = adminRole.Name });
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(admin.Id);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(admin.Id, Arg.Any<CancellationToken>())
            .Returns([.. Permission.All.Select(p => p.Code)]);

        var handler = new AssignUserRolesCommandHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand { UserId = admin.Id, RoleNames = [] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.LastAdministrator);
    }
}
