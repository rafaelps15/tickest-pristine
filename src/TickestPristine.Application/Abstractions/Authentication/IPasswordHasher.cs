namespace TickestPristine.Application.Abstractions.Authentication;

/// <summary>
/// Gera e confere o hash das senhas; o texto da senha nunca é gravado.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
