namespace TickestPristine.Application.Abstractions.Storage;

/// <summary>
/// Armazenamento dos arquivos de anexo; o banco guarda só a chave de cada arquivo, criada por <see cref="CreateKey"/>.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Cria a chave de um arquivo novo a partir do nome original, sem gravar nada: o envio é conferido antes de o arquivo
    /// chegar ao armazenamento.
    /// </summary>
    string CreateKey(string fileName);

    /// <summary>
    /// Grava o arquivo com a chave criada por <see cref="CreateKey"/>. Se a gravação falha no meio, o arquivo pela metade
    /// é apagado; uma chave que já existe falha sem tocar no arquivo dela.
    /// </summary>
    Task SaveAsync(Stream content, string storageKey, CancellationToken cancellationToken);

    /// <summary>
    /// Abre o arquivo para leitura; devolve nulo quando ele não existe mais no armazenamento.
    /// </summary>
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>
    /// Apaga o arquivo; chave que não existe não gera erro.
    /// </summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
