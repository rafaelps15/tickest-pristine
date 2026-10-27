using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace TickestPristine.Infrastructure.Notifications;

/// <summary>
/// Recusa na inicialização um endereço de ativação inválido: um link errado geraria convites inúteis. Fora de
/// Development, o endereço precisa ser HTTPS.
/// </summary>
internal sealed class InvitationOptionsValidator(IHostEnvironment environment) : IValidateOptions<InvitationOptions>
{
    private const string SettingName = $"{InvitationOptions.SectionName}:{nameof(InvitationOptions.ActivationUrl)}";

    public ValidateOptionsResult Validate(string? name, InvitationOptions options)
    {
        if (!Uri.TryCreate(options.ActivationUrl, UriKind.Absolute, out Uri? url) ||
            url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp)
        {
            return ValidateOptionsResult.Fail($"'{SettingName}' precisa ser um endereço absoluto HTTP ou HTTPS.");
        }

        if (!string.IsNullOrEmpty(url.Fragment))
        {
            return ValidateOptionsResult.Fail($"'{SettingName}' não pode ter fragmento (#): o token vai nele.");
        }

        if (url.Scheme != Uri.UriSchemeHttps && !environment.IsDevelopment())
        {
            return ValidateOptionsResult.Fail($"'{SettingName}' precisa ser HTTPS fora de Development.");
        }

        return ValidateOptionsResult.Success;
    }
}
