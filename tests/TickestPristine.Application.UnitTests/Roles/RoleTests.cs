using TickestPristine.Domain.Permissions;
using TickestPristine.Domain.Roles;

namespace TickestPristine.Application.UnitTests.Roles;

public sealed class RoleTests
{
    [Fact]
    public void Create_Should_SetIdentityAndRaiseCreatedEvent_WhenCalled()
    {
        // Arrange
        // Act
        var role = Role.Create("Atendimento");

        // Assert
        role.Id.ShouldNotBe(Guid.Empty);
        role.Name.ShouldBe("Atendimento");
        role.DomainEvents.Single().ShouldBeOfType<RoleCreatedDomainEvent>().RoleId.ShouldBe(role.Id);
    }
    [Fact]
    public void ReplacePermissions_Should_ReplaceSetAndRaiseEvent_WhenPermissionsAreValid()
    {
        // Arrange
        var role = Role.Create("Atendimento");
        role.ReplacePermissions([PermissionCodes.Tickets.Create, PermissionCodes.Tickets.ViewOwn], []).IsSuccess.ShouldBeTrue();
        role.ClearDomainEvents();

        // Act
        Result<RolePermissionsChange> result = role.ReplacePermissions(
            [PermissionCodes.Tickets.Create, PermissionCodes.Tickets.Manage],
            []);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Added.ShouldBe([PermissionCodes.Tickets.Manage]);
        result.Value.Removed.ShouldBe([PermissionCodes.Tickets.ViewOwn]);
        role.Permissions.Select(permission => permission.PermissionCode)
            .ShouldBe([PermissionCodes.Tickets.Create, PermissionCodes.Tickets.Manage], ignoreOrder: true);
        RolePermissionsAssignedDomainEvent domainEvent = role.DomainEvents.Single().ShouldBeOfType<RolePermissionsAssignedDomainEvent>();
        domainEvent.RoleId.ShouldBe(role.Id);
        domainEvent.PreviousPermissionCodes.ShouldBe([PermissionCodes.Tickets.Create, PermissionCodes.Tickets.ViewOwn]);
        domainEvent.PermissionCodes.ShouldBe([PermissionCodes.Tickets.Create, PermissionCodes.Tickets.Manage]);
    }

    [Fact]
    public void ReplacePermissions_Should_NotRaiseEventOrIncrementVersion_WhenSetIsUnchanged()
    {
        // Arrange
        var role = Role.Create("Atendimento");
        role.ReplacePermissions([PermissionCodes.Tickets.Create], []).IsSuccess.ShouldBeTrue();
        role.ClearDomainEvents();
        int versionBefore = role.Version;

        // Act
        Result<RolePermissionsChange> result = role.ReplacePermissions([PermissionCodes.Tickets.Create], []);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Added.ShouldBeEmpty();
        result.Value.Removed.ShouldBeEmpty();
        role.Version.ShouldBe(versionBefore);
        role.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void ReplacePermissions_Should_ReturnManagedErrorWithoutChangingState_WhenRoleIsAdministrator()
    {
        // Arrange
        var role = Role.Create(RoleNames.Administrator);
        role.ClearDomainEvents();

        // Act
        Result<RolePermissionsChange> result = role.ReplacePermissions([PermissionCodes.Tickets.Create], []);

        // Assert
        result.Error.ShouldBe(RoleErrors.AdministratorRoleIsManaged);
        role.Permissions.ShouldBeEmpty();
        role.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void ReplacePermissions_Should_ReturnPrivilegeEscalationWithoutChangingState_WhenCallerLacksChangedAdministrativePermission()
    {
        // Arrange
        var role = Role.Create("Atendimento");
        role.ReplacePermissions([PermissionCodes.Tickets.Create], []).IsSuccess.ShouldBeTrue();
        role.ClearDomainEvents();
        int versionBefore = role.Version;

        // Act
        Result<RolePermissionsChange> result = role.ReplacePermissions(
            [PermissionCodes.Tickets.Create, PermissionCodes.Users.AssignRoles],
            [PermissionCodes.Roles.Manage]);

        // Assert
        result.Error.ShouldBe(RoleErrors.PrivilegeEscalation([PermissionCatalog.GetDisplayName(PermissionCodes.Users.AssignRoles)]));
        role.Permissions.Select(permission => permission.PermissionCode).ShouldBe([PermissionCodes.Tickets.Create]);
        role.Version.ShouldBe(versionBefore);
        role.DomainEvents.ShouldBeEmpty();
    }
}










