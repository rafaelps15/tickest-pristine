using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

public sealed class Role : Entity, IAuditable
{
    public Guid Id { get; set; }
    public string Name { get; set; }

    /// <summary>
    /// Role que quem se cadastra recebe. No máximo uma role é a padrão.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Token de concorrência, incrementado quando um administrador perde o acesso (ver AdministratorGuard).
    /// </summary>
    public int Version { get; set; }
}
