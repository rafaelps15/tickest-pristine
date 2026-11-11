using TickestPristine.Domain.Permissions;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Roles.Rules;
using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

public sealed class Role : Entity
{
    private readonly List<RolePermission> _permissions = [];

    private Role() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public int Version { get; private set; }
    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    /// <summary>Cria a função e registra o evento de criação do agregado.</summary>
    public static Role Create(string name)
    {
        var role = new Role { Id = Guid.NewGuid(), Name = name };
        role.Raise(new RoleCreatedDomainEvent(role.Id));

        return role;
    }

    /// <summary>Substitui o conjunto de permissões; falha sem alterar estado quando a função ou a RN-17 impedem a mudança.</summary>
    public Result<RolePermissionsChange> ReplacePermissions(
        IEnumerable<string> permissionCodes,
        IEnumerable<string> callerPermissionCodes)
    {
        if (Name == RoleNames.Administrator)
        {
            return Result.Failure<RolePermissionsChange>(RoleErrors.AdministratorRoleIsManaged);
        }

        HashSet<string> currentCodes = [.. _permissions.Select(permission => permission.PermissionCode)];
        HashSet<string> requestedCodes = [.. permissionCodes];
        HashSet<string> changedCodes = [.. currentCodes];
        changedCodes.SymmetricExceptWith(requestedCodes);
        HashSet<string> callerCodes = [.. callerPermissionCodes];

        Result rule = CheckRule(new OnlyPermissionHolderCanGrantOrRemoveAdministrativePermissionRule(changedCodes, callerCodes));
        if (rule.IsFailure)
        {
            return Result.Failure<RolePermissionsChange>(rule.Error);
        }

        List<string> added = [.. requestedCodes.Except(currentCodes).Order(StringComparer.Ordinal)];
        List<string> removed = [.. currentCodes.Except(requestedCodes).Order(StringComparer.Ordinal)];

        if (added.Count == 0 && removed.Count == 0)
        {
            return Result.Success(new RolePermissionsChange(added, removed));
        }

        List<string> previousPermissionCodes = [.. currentCodes.Order(StringComparer.Ordinal)];

        _permissions.RemoveAll(permission => removed.Contains(permission.PermissionCode, StringComparer.Ordinal));
        _permissions.AddRange(added.Select(code => RolePermission.Create(Name, code)));
        Version++;
        Raise(new RolePermissionsAssignedDomainEvent(Id, previousPermissionCodes, [.. requestedCodes.Order(StringComparer.Ordinal)]));

        return Result.Success(new RolePermissionsChange(added, removed));
    }
}

public sealed record RolePermissionsChange(IReadOnlyCollection<string> Added, IReadOnlyCollection<string> Removed);
