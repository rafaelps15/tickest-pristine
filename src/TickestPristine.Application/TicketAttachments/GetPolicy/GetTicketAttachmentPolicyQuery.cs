using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.TicketAttachments.GetPolicy;

/// <summary>
/// Limites e tipos de arquivo aceitos nos anexos, para o front validar antes do envio.
/// </summary>
public sealed record GetTicketAttachmentPolicyQuery : IQuery<TicketAttachmentPolicyResponse>;
