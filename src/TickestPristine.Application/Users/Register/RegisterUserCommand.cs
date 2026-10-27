using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.Register;

/// <summary>
/// Cadastra um usuário sem senha: ele recebe um convite por e-mail e define a própria senha na ativação.
/// </summary>
public sealed record RegisterUserCommand(string Email, string FirstName, string LastName) : ICommand<Guid>;
