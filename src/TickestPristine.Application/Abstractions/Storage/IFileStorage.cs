namespace TickestPristine.Application.Abstractions.Storage;

public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken);

    /// <summary>
    /// Abre o arquivo para leitura; devolve nulo quando ele não existe mais no armazenamento.
    /// </summary>
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>
    /// Apaga o arquivo; chave que não existe não gera erro.
    /// </summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
