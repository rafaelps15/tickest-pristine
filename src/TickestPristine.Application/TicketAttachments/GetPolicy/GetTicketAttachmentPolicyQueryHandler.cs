using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketAttachments.GetPolicy;

/// <summary>
/// Lê as mesmas constantes usadas na validação do envio: a política informada nunca diverge da aplicada.
/// </summary>
internal sealed class GetTicketAttachmentPolicyQueryHandler
    : IQueryHandler<GetTicketAttachmentPolicyQuery, TicketAttachmentPolicyResponse>
{
    public Task<Result<TicketAttachmentPolicyResponse>> Handle(
        GetTicketAttachmentPolicyQuery query,
        CancellationToken cancellationToken)
    {
        var policy = new TicketAttachmentPolicyResponse
        {
            MaxFileSizeBytes = TicketAttachment.MaxFileSizeBytes,
            MaxAttachmentsPerTicket = TicketAttachment.MaxAttachmentsPerTicket,
            MaxTotalBytesPerTicket = TicketAttachment.MaxTotalBytesPerTicket,
            AllowedTypes = TicketAttachment.AllowedTypes
                .Select(type => new AllowedAttachmentTypeResponse
                {
                    ContentType = type.ContentType,
                    Extensions = [.. type.Extensions]
                })
                .ToList()
        };

        return Task.FromResult<Result<TicketAttachmentPolicyResponse>>(policy);
    }
}
