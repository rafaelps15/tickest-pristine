using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Audit;

/// <summary>
/// Marca, na configuração do EF, as colunas que ficam fora do log de auditoria. Uma propriedade com nome de segredo sem
/// a marca faz o teste de arquitetura da auditoria falhar.
/// </summary>
internal static class AuditPropertyExtensions
{
    private const string NotAuditedAnnotation = "Tickest:NotAudited";

    /// <summary>
    /// O valor da coluna nunca vai para o log de auditoria; o nome dela continua em <c>AffectedColumns</c> quando muda.
    /// </summary>
    public static PropertyBuilder<TProperty> IsNotAudited<TProperty>(this PropertyBuilder<TProperty> builder) =>
        builder.HasAnnotation(NotAuditedAnnotation, true);

    public static bool IsAudited(this IReadOnlyProperty property) =>
        property.FindAnnotation(NotAuditedAnnotation) is null;
}
