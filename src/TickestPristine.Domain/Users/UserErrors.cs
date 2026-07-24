using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public static class UserErrors
{
    public static Error NotFound(Guid userId) => Error.NotFound(
        "Users.NotFound",
        $"O usuário com o Id = '{userId}' não foi encontrado");

    public static Error Unauthorized() => Error.Forbidden(
        "Users.Unauthorized",
        "Você não tem permissão para executar esta ação.");

    public static readonly Error NotFoundByEmail = Error.NotFound(
        "Users.NotFoundByEmail",
        "Usuário com o e-mail informado não foi encontrado");

    public static readonly Error EmailNotUnique = Error.Conflict(
        "Users.EmailNotUnique",
        "O e-mail informado já está em uso");

    public static readonly Error InvalidRefreshToken = Error.Problem(
        "Users.InvalidRefreshToken",
        "O refresh token informado é inválido ou expirou");
}
