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

    public static Error CannotManagePrivilegedUser(IEnumerable<string> permissionNames) => Error.Forbidden(
        "Users.CannotManagePrivilegedUser",
        $"Você não pode desativar nem reativar quem tem permissões administrativas que você não possui: {string.Join(", ", permissionNames)}");

    public static readonly Error ReadForbidden = Error.Forbidden(
        "Users.ReadForbidden",
        "Você não tem permissão para buscar usuários.");

    public static Error InvalidSearchTerm(int minLength, int maxLength) => Error.Problem(
        "Users.InvalidSearchTerm",
        $"O termo de busca deve ter entre {minLength} e {maxLength} caracteres.");

    public static readonly Error LockedOut = Error.Forbidden(
        "Users.LockedOut",
        "Muitas tentativas de login sem sucesso. A conta está bloqueada por alguns minutos; tente de novo mais tarde.");

    public static readonly Error NotFoundByEmail = Error.NotFound(
        "Users.NotFoundByEmail",
        "Usuário com o e-mail informado não foi encontrado");

    public static readonly Error InvalidCredentials = Error.Problem(
        "Users.InvalidCredentials",
        "E-mail ou senha inválidos");

    public static readonly Error EmailNotUnique = Error.Conflict(
        "Users.EmailNotUnique",
        "O e-mail informado já está em uso");

    public static readonly Error InvalidCurrentPassword = Error.Problem(
        "Users.InvalidCurrentPassword",
        "A senha atual informada está incorreta");

    public static readonly Error InvalidRefreshToken = Error.Problem(
        "Users.InvalidRefreshToken",
        "O refresh token informado é inválido ou expirou");

    public static readonly Error AlreadyDeactivated = Error.Conflict(
        "Users.AlreadyDeactivated",
        "O usuário já está desativado.");

    public static readonly Error CannotDeactivateSelf = Error.Problem(
        "Users.CannotDeactivateSelf",
        "Você não pode desativar a sua própria conta.");

    public static readonly Error AlreadyActive = Error.Conflict(
        "Users.AlreadyActive",
        "O usuário já está ativo.");

    public static readonly Error Deactivated = Error.Forbidden(
        "Users.Deactivated",
        "O usuário está desativado.");

    public static readonly Error PasswordChangeRequired = Error.Forbidden(
        "Users.PasswordChangeRequired",
        "É preciso definir uma nova senha antes de acessar o sistema.");

    public static readonly Error InvalidActivationToken = Error.Problem(
        "Users.InvalidActivationToken",
        "Link inválido ou expirado. Solicite um novo convite ao administrador.");

    public static readonly Error AlreadyActivated = Error.Conflict(
        "Users.AlreadyActivated",
        "A conta deste usuário já foi ativada.");

    public static readonly Error InvitationUserDeactivated = Error.Conflict(
        "Users.InvitationUserDeactivated",
        "O usuário está desativado. Reative-o antes de enviar um novo convite.");

    public static Error InvitationLimitReached(int maxInvitations, int windowInHours) => Error.Conflict(
        "Users.InvitationLimitReached",
        $"Este usuário já recebeu {maxInvitations} convites nas últimas {windowInHours} horas. Tente de novo mais tarde.");

    public static readonly Error PasswordChangeNotRequired = Error.Conflict(
        "Users.PasswordChangeNotRequired",
        "Não há troca de senha pendente para este usuário.");
}
