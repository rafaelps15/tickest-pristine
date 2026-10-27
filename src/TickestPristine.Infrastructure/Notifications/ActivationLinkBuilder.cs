using Microsoft.Extensions.Options;
using TickestPristine.Application.Abstractions.Notifications;

namespace TickestPristine.Infrastructure.Notifications;

internal sealed class ActivationLinkBuilder(IOptions<InvitationOptions> options) : IActivationLinkBuilder
{
    public string Build(string token) => $"{options.Value.ActivationUrl}#token={Uri.EscapeDataString(token)}";
}
