namespace TickestPristine.Application.Abstractions.Storage;

/// <summary>
/// Confere se o conteúdo de um anexo corresponde ao tipo declarado pelo cliente, pelos primeiros bytes do arquivo.
/// </summary>
public interface IAttachmentContentInspector
{
    /// <summary>
    /// Indica se o conteúdo corresponde ao tipo informado; um tipo que o inspetor não reconhece não corresponde. Lê o
    /// começo do arquivo e devolve o fluxo à posição em que estava; exige um fluxo posicionável.
    /// </summary>
    Task<bool> MatchesAsync(Stream content, string contentType, CancellationToken cancellationToken);
}
