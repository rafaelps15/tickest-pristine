using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

/// <summary>Permissão de referência definida pelo catálogo e persistida por migrations.</summary>
public sealed class Permission : Entity
{
    private Permission() { }

    public Guid Id { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string Group { get; private set; }
    public bool IsAdministrative { get; private set; }

    /// <summary>Cria a permissão de referência e registra o evento de criação.</summary>
    public static Permission Create(Guid id, string code, string name, string description, string group, bool isAdministrative)
    {
        var permission = new Permission
        {
            Id = id,
            Code = code,
            Name = name,
            Description = description,
            Group = group,
            IsAdministrative = isAdministrative
        };

        permission.Raise(new PermissionCreatedDomainEvent(permission.Id));

        return permission;
    }
}
