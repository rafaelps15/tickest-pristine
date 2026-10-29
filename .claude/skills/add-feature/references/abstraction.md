# Modelos de abstração

O trabalho técnico de que um handler precisa (hash, tokens, hora, arquivos, e-mail) é uma abstração: a interface em `src/Application/Abstractions/{Area}/`, a implementação em `src/Infrastructure/{Area}/`.

## Interface

`src/Application/Abstractions/Authentication/IPasswordHasher.cs`:

```csharp
namespace Application.Abstractions.Authentication;

/// <summary>Gera e confere hashes de senha.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>Indica se a senha corresponde ao hash.</summary>
    bool Verify(string password, string passwordHash);
}
```

- `public interface I{Noun}`, com o nome do que faz (`PasswordHasher`, `TokenProvider`, `DateTimeProvider`) — não `*Service`, `*Helper`, `*Manager`.
- Poucos membros, cada um um verbo (`Hash`, `Verify`, `Create`). Sufixo `Async` só quando devolve `Task`.

## Implementação

`src/Infrastructure/Authentication/PasswordHasher.cs`:

```csharp
using System.Security.Cryptography;
using Application.Abstractions.Authentication;

namespace Infrastructure.Authentication;

internal sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 500000;

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    public string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        return $"{Convert.ToHexString(hash)}-{Convert.ToHexString(salt)}";
    }

    /// <summary>Indica se a senha corresponde ao hash, comparando em tempo constante.</summary>
    public bool Verify(string password, string passwordHash)
    {
        string[] parts = passwordHash.Split('-');
        byte[] hash = Convert.FromHexString(parts[0]);
        byte[] salt = Convert.FromHexString(parts[1]);

        byte[] inputHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        return CryptographicOperations.FixedTimeEquals(hash, inputHash);
    }
}
```

- `internal sealed`, namespace no escopo do arquivo, `using` ordenados com `System` primeiro.
- Dependências pelo construtor primário (`TokenProvider(IConfiguration configuration)`).
- Valores fixos como `private const` / `private static readonly` no topo, com nome (`SaltSize`, não `16` solto).
- Tipos explícitos para os tipos nativos (`byte[] salt`, `string[] parts`); `var` só quando o tipo está à direita (`var credentials = new SigningCredentials(...)`).
- Uma linha em branco entre os passos, `return` separado do trabalho anterior.
- Identificadores em inglês; um `<summary>` curto em português só onde explica algo: em toda interface e nos métodos cujo comportamento não é óbvio pelo nome e pela assinatura (o que acontece em falha, quando devolve `false`, efeitos colaterais). Nada em propriedades, construtores, DTOs, endpoints, testes e métodos simples; nunca vazio. Qualquer outro comentário só onde o código não se explica, também em português (`// Reinicia o status de conclusão na cópia`).
- Métodos `private static` pequenos em vez de um método longo com `if`s aninhados; uma expressão `switch` para escolher por valor (`error.Type switch { ... }` no `CustomResults.Problem`).
- Quando uma biblioteca conhecida já faz o trabalho, a implementação a envolve (`TokenProvider` envolve `JsonWebTokenHandler`); a biblioteca nunca vaza para a Application.

## Registro

`src/Infrastructure/DependencyInjection.cs`, agrupado com a área:

```csharp
services.AddSingleton<IPasswordHasher, PasswordHasher>();
services.AddSingleton<ITokenProvider, TokenProvider>();
```

`AddSingleton` quando não guarda estado por requisição; `AddScoped` quando depende da requisição (`services.AddScoped<IUserContext, UserContext>();`).

## Uso no handler

Injetada pelo construtor primário do handler (`RegisterUserCommandHandler(IApplicationDbContext context, IPasswordHasher passwordHasher)`) e chamada em uma linha, com o resultado indo para a entidade, nunca para uma propriedade dela: `User.Create(command.Email, passwordHasher.Hash(command.Password))`. Os testes unitários do handler a substituem por `Substitute.For<I{Noun}>()`.
