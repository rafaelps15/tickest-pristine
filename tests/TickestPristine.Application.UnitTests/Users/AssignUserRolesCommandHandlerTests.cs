using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Users.AssignRoles;
using TickestPristine.Domain.Permissions;
using TickestPristine.Application.Roles;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.Application.Users;

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
        permissionProvider.GetCurrentForUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        AssignUserRolesCommandHandler handler = CreateHandler(context, userContext, permissionProvider);

        var command = new AssignUserRolesCommand(Guid.NewGuid(), ["Gestor"]);

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
        permissionProvider.GetCurrentForUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        AssignUserRolesCommandHandler handler = CreateHandler(context, userContext, permissionProvider);

        var command = new AssignUserRolesCommand(user.Id, ["Gestor"]);

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
        permissionProvider.GetCurrentForUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        AssignUserRolesCommandHandler handler = CreateHandler(context, userContext, permissionProvider);

        var command = new AssignUserRolesCommand(user.Id, [newRole.Name]);

        using var requestCancellation = new CancellationTokenSource();

        // Act
        Result result = await handler.Handle(command, requestCancellation.Token);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        List<string> assignedRoleNames = await context.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.RoleName)
            .ToListAsync();
        assignedRoleNames.ShouldBe([newRole.Name]);

        User updated = await context.Users.SingleAsync(u => u.Id == user.Id);
        updated.DomainEvents.ShouldContain(domainEvent => domainEvent is UserRolesAssignedDomainEvent);

        await permissionProvider.Received(1).InvalidateAsync(user.Id, CancellationToken.None);
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

        Role managerRole = CreateRole("Gestor");
        context.Roles.Add(managerRole);
        context.RolePermissions.Add(RolePermission.Create(managerRole.Name, PermissionCodes.Users.Read));
        context.RolePermissions.Add(RolePermission.Create(managerRole.Name, PermissionCodes.Roles.Manage));

        await context.SaveChangesAsync();

        var callerId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetCurrentForUserIdAsync(callerId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Users.Read, PermissionCodes.Users.AssignRoles]);

        AssignUserRolesCommandHandler handler = CreateHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand(user.Id, [managerRole.Name]);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.PrivilegeEscalation([PermissionCatalog.GetDisplayName(PermissionCodes.Roles.Manage)]));

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
        context.RolePermissions.Add(RolePermission.Create(agentRole.Name, PermissionCodes.Tickets.Manage));

        await context.SaveChangesAsync();

        var callerId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetCurrentForUserIdAsync(callerId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Users.AssignRoles]);

        AssignUserRolesCommandHandler handler = CreateHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand(user.Id, [agentRole.Name]);

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
        context.RolePermissions.Add(RolePermission.Create(adminRole.Name, PermissionCodes.Roles.Manage));
        context.RolePermissions.Add(RolePermission.Create(adminRole.Name, PermissionCodes.Users.AssignRoles));
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = admin.Id, RoleName = adminRole.Name });

        await context.SaveChangesAsync();

        var callerId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetCurrentForUserIdAsync(callerId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Users.AssignRoles]);

        AssignUserRolesCommandHandler handler = CreateHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand(admin.Id, []);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.PrivilegeEscalation([PermissionCatalog.GetDisplayName(PermissionCodes.Roles.Manage)]));

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

        Role managerRole = CreateRole("Gestor");
        Role agentRole = CreateRole("Agente");
        context.Roles.Add(managerRole);
        context.Roles.Add(agentRole);
        context.RolePermissions.Add(RolePermission.Create(managerRole.Name, PermissionCodes.Roles.Manage));
        context.RolePermissions.Add(RolePermission.Create(agentRole.Name, PermissionCodes.Tickets.Manage));
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = user.Id, RoleName = managerRole.Name });

        await context.SaveChangesAsync();

        var callerId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetCurrentForUserIdAsync(callerId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Users.AssignRoles]);

        AssignUserRolesCommandHandler handler = CreateHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand(user.Id, [managerRole.Name, agentRole.Name]);

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

        Role adminRole = CreateRole(RoleNames.Administrator);
        context.Roles.Add(adminRole);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = admin.Id, RoleName = adminRole.Name });
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(admin.Id);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetCurrentForUserIdAsync(admin.Id, Arg.Any<CancellationToken>())
            .Returns([.. PermissionCatalog.All.Select(p => p.Code)]);

        AssignUserRolesCommandHandler handler = CreateHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand(admin.Id, []);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.LastAdministrator);
        (await context.UserRoles.AnyAsync(ur => ur.UserId == admin.Id && ur.RoleName == adminRole.Name)).ShouldBeTrue();
        context.Entry(await context.Roles.SingleAsync(r => r.Name == adminRole.Name)).Property(role => role.Version).CurrentValue.ShouldBe(0);
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

        Role adminRole = CreateRole(RoleNames.Administrator);
        context.Roles.Add(adminRole);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = admin.Id, RoleName = adminRole.Name });
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = otherAdmin.Id, RoleName = adminRole.Name });
        context.UserCredentials.Add(new UserCredential { Id = Guid.NewGuid(), UserId = otherAdmin.Id, PasswordHash = "hash" });
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(otherAdmin.Id);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetCurrentForUserIdAsync(otherAdmin.Id, Arg.Any<CancellationToken>())
            .Returns([.. PermissionCatalog.All.Select(p => p.Code)]);

        AssignUserRolesCommandHandler handler = CreateHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand(admin.Id, []);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.UserRoles.AnyAsync(ur => ur.UserId == admin.Id)).ShouldBeFalse();

        Role administratorRole = await context.Roles.SingleAsync(r => r.Name == adminRole.Name);
        context.Entry(administratorRole).Property(role => role.Version).CurrentValue.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_ReturnLastAdministrator_WhenOtherAdministratorIsDeactivated()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User admin = CreateUser(isActive: true);
        User deactivatedAdmin = CreateUser(isActive: false);
        context.Users.AddRange(admin, deactivatedAdmin);

        Role adminRole = CreateRole(RoleNames.Administrator);
        context.Roles.Add(adminRole);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = admin.Id, RoleName = adminRole.Name });
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = deactivatedAdmin.Id, RoleName = adminRole.Name });
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(admin.Id);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetCurrentForUserIdAsync(admin.Id, Arg.Any<CancellationToken>())
            .Returns([.. PermissionCatalog.All.Select(p => p.Code)]);

        AssignUserRolesCommandHandler handler = CreateHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand(admin.Id, []);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.LastAdministrator);
    }

    [Fact]
    public async Task Handle_Should_ChangeNothing_WhenRolesAreAlreadyTheCurrentOnes()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = CreateUser(isActive: true);
        context.Users.Add(user);

        Role agentRole = CreateRole("Agente");
        context.Roles.Add(agentRole);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = user.Id, RoleName = agentRole.Name });
        await context.SaveChangesAsync();

        IPermissionProvider permissionProvider = CreatePermissionProvider();
        AssignUserRolesCommandHandler handler = CreateHandler(context, Substitute.For<IUserContext>(), permissionProvider);

        // Act
        Result result = await handler.Handle(new AssignUserRolesCommand(user.Id, [agentRole.Name]), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.UserRoles.Where(ur => ur.UserId == user.Id).Select(ur => ur.RoleName).ToListAsync())
            .ShouldBe([agentRole.Name]);
        (await context.Users.SingleAsync(u => u.Id == user.Id)).DomainEvents.ShouldBeEmpty();
        await permissionProvider.DidNotReceive().InvalidateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnLastAdministrator_WhenOtherAdministratorIsPendingActivation()
    {
        // Arrange: o outro administrador está ativo, mas ainda não definiu a senha (convite pendente)
        await using TestDbContext context = CreateDbContext();
        User admin = CreateUser(isActive: true);
        User pendingAdmin = CreateUser(isActive: true);
        context.Users.AddRange(admin, pendingAdmin);

        Role adminRole = CreateRole(RoleNames.Administrator);
        context.Roles.Add(adminRole);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = admin.Id, RoleName = adminRole.Name });
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = pendingAdmin.Id, RoleName = adminRole.Name });
        context.UserCredentials.Add(new UserCredential { Id = Guid.NewGuid(), UserId = admin.Id, PasswordHash = "hash" });
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(admin.Id);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetCurrentForUserIdAsync(admin.Id, Arg.Any<CancellationToken>())
            .Returns([.. PermissionCatalog.All.Select(p => p.Code)]);

        AssignUserRolesCommandHandler handler = CreateHandler(context, userContext, permissionProvider);
        var command = new AssignUserRolesCommand(admin.Id, []);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.LastAdministrator);
        (await context.UserRoles.AnyAsync(ur => ur.UserId == admin.Id && ur.RoleName == adminRole.Name)).ShouldBeTrue();
    }

    private static AssignUserRolesCommandHandler CreateHandler(
        TestDbContext context,
        IUserContext userContext,
        IPermissionProvider permissionProvider) =>
        new(
            context,
            userContext,
            permissionProvider,
            new PrivilegeEscalationCheck(permissionProvider),
            new LastAdministratorCheck(context));
}


