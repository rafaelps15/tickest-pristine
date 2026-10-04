using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed class UserCredential : Entity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PasswordHash { get; set; }

    /// <summary>
    /// Senha provisória (ex.: a do primeiro administrador): o login só entrega tokens depois que ela for trocada.
    /// </summary>
    public bool MustChangePassword { get; set; }
}
