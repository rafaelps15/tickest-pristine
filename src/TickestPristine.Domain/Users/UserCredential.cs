using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed class UserCredential : Entity, IAuditable
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

    /// <summary>
    /// Muda a cada troca de senha. O refresh token guarda o valor de quando a sessão foi aberta e a renovação recusa o
    /// token quando os dois não batem: um login com a senha antiga que termine depois da troca não gera sessão válida.
    /// </summary>
    public Guid SecurityStamp { get; set; }

    /// <summary>
    /// Senhas erradas seguidas desde o último login correto ou o último bloqueio.
    /// </summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>
    /// Fim do bloqueio temporário por excesso de senhas erradas; nulo quando a conta não está bloqueada.
    /// </summary>
    public DateTime? LockoutEndsAtUtc { get; set; }
}
