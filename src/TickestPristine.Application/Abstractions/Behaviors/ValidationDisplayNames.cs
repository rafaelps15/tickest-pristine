using System.Linq.Expressions;
using System.Reflection;

using TickestPristine.Application.Permissions;

using TickestPristine.Application.Roles;

namespace TickestPristine.Application.Abstractions.Behaviors;

/// <summary>
/// Nome em português de cada propriedade validada, usado nas mensagens padrão do FluentValidation: sem ele, o usuário
/// leria o nome da propriedade em inglês (ex.: "'First Name' deve ser informado.").
/// </summary>
internal static class ValidationDisplayNames
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.Ordinal)
    {
        ["AssignedToUserId"] = "Responsável",
        ["AttachmentId"] = "Anexo",
        ["Content"] = "Conteúdo",
        ["ContentType"] = "Tipo do arquivo",
        ["CurrentPassword"] = "Senha atual",
        ["DepartmentId"] = "Departamento",
        ["Description"] = "Descrição",
        ["Email"] = "E-mail",
        ["FileName"] = "Nome do arquivo",
        ["FileSizeBytes"] = "Tamanho do arquivo",
        ["FirstName"] = "Nome",
        ["LastName"] = "Sobrenome",
        ["MessageId"] = "Mensagem",
        ["Name"] = "Nome",
        ["NewPassword"] = "Nova senha",
        ["Password"] = "Senha",
        ["PermissionCodes"] = "Permissões",
        ["Priority"] = "Prioridade",
        ["Reason"] = "Motivo",
        ["RefreshToken"] = "Refresh token",
        ["RequesterId"] = "Solicitante",
        ["ResponsibleUserId"] = "Responsável",
        ["RoleName"] = "Função",
        ["RoleNames"] = "Funções",
        ["SectorId"] = "Setor",
        ["Status"] = "Status",
        ["TicketId"] = "Chamado",
        ["Token"] = "Link de ativação",
        ["Title"] = "Título",
        ["UserId"] = "Usuário"
    };

    /// <summary>
    /// Resolvedor para <c>ValidatorOptions.Global.DisplayNameResolver</c>; devolve nulo para as propriedades fora da
    /// lista, e aí o FluentValidation usa o próprio nome da propriedade.
    /// </summary>
    public static string? Resolve(Type type, MemberInfo? member, LambdaExpression? expression) =>
        member is not null && Names.TryGetValue(member.Name, out string? name) ? name : null;
}
