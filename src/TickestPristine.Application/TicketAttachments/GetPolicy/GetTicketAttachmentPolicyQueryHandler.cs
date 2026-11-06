using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.TicketAttachments;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketAttachments.GetPolicy;

/// <summary>
/// Lê a <see cref="TicketAttachmentPolicy"/>, a mesma que confere o envio: a política informada nunca diverge da aplicada.
/// </summary>
internal sealed class GetTicketAttachmentPolicyQueryHandler
    : IQueryHandler<GetTicketAttachmentPolicyQuery, TicketAttachmentPolicyResponse>
{
    public Task<Result<TicketAttachmentPolicyResponse>> Handle(
        GetTicketAttachmentPolicyQuery request,
        CancellationToken cancellationToken)
    {
        var policy = new TicketAttachmentPolicyResponse
        {
            MaxFileSizeBytes = TicketAttachmentPolicy.MaxFileSizeBytes,
            MaxAttachmentsPerTicket = TicketAttachmentPolicy.MaxAttachmentsPerTicket,
            MaxTotalBytesPerTicket = TicketAttachmentPolicy.MaxTotalBytesPerTicket,
            AllowedTypes = TicketAttachmentPolicy.AllowedTypes
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
