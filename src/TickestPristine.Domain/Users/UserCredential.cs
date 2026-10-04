using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed class UserCredential : Entity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PasswordHash { get; set; }

    /// <summary>
    /// Senha provisória (ex.: a do primeiro administrador): o login só entrega tokens depois que ela for trocada.
    /// Quem ligar a pendência numa conta que já existe precisa revogar os refresh tokens dela, porque a renovação de
    /// token não confere esta marcação.
    /// </summary>
    public bool MustChangePassword { get; set; }
}
