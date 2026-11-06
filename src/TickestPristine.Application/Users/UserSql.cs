using TickestPristine.Application.Permissions;

namespace TickestPristine.Application.Users;

/// <summary>
/// Regras de usuário escritas como trechos de SQL com nome, para as consultas em Dapper, sobre a tabela
/// <c>public.users</c> com o alias <c>u</c>. Um teste de integração confere que cada trecho concorda com as outras
/// leituras da mesma regra.
/// </summary>
internal static class UserSql
{
    /// <summary>
    /// Conta pendente de ativação: usuário ativo que ainda não definiu a senha (sem credencial).
    /// </summary>
    internal const string IsPendingActivation =
        "u.is_active AND NOT EXISTS (SELECT 1 FROM public.user_credentials c WHERE c.user_id = u.id)";

    /// <summary>
    /// Validade do convite da conta pendente: a maior entre os convites ainda utilizáveis (sem uso, sem revogação e
    /// válidos em <c>@NowUtc</c>); nula quando a conta não está pendente ou quando nenhum convite serve mais.
    /// </summary>
    internal const string PendingInvitationExpiresAtUtc =
        $"""
         CASE WHEN {IsPendingActivation} THEN (
             SELECT max(i.expires_at_utc)
             FROM public.user_invitations i
             WHERE i.user_id = u.id
               AND i.used_at_utc IS NULL
               AND i.revoked_at_utc IS NULL
               AND i.expires_at_utc > @NowUtc::timestamptz)
         END
         """;

    /// <summary>
    /// Conta utilizável para receber chamados: hoje, o usuário ativo.
    /// </summary>
    internal const string IsUsable = "u.is_active";

    /// <summary>
    /// Quem atende chamados: conta utilizável com a permissão efetiva <c>tickets:manage</c>, vinda de qualquer uma das
    /// suas roles, lida das tabelas (sem o cache de permissões), a mesma regra do <c>TicketAgentLookup</c>.
    /// </summary>
    internal const string IsTicketAgent =
        $"""
         {IsUsable} AND EXISTS (
             SELECT 1
             FROM public.user_roles ur
             JOIN public.role_permissions rp ON rp.role_name = ur.role_name
             WHERE ur.user_id = u.id AND rp.permission_code = '{PermissionCodes.Tickets.Manage}')
         """;
}
