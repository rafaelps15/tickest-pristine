using TickestPristine.Application.Abstractions.Storage;
using Microsoft.Extensions.Options;

namespace TickestPristine.Infrastructure.Storage;

/// <summary>
/// Guarda os arquivos dos anexos numa pasta do servidor. A chave de cada arquivo é um Guid com a extensão original.
/// </summary>
internal sealed class LocalFileStorage(IOptions<FileStorageOptions> options) : IFileStorage
{
    private const int BufferSize = 81920;

    // Sem o separador no fim: com "C:\anexos\\" configurado, a checagem de caminho contido recusaria todos os arquivos.
    private readonly string _rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.Value.RootPath));

    public string CreateKey(string fileName) => $"{Guid.NewGuid():N}{SanitizeExtension(Path.GetExtension(fileName))}";

    public async Task SaveAsync(Stream content, string storageKey, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_rootPath);

        string path = ResolveContainedPath(storageKey);

        // Fora do try: com a chave já usada, o CreateNew falha antes de existir algo desta gravação para apagar.
        var fileStream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous);

        try
        {
            await using (fileStream)
            {
                await content.CopyToAsync(fileStream, cancellationToken);
            }
        }
        catch
        {
            // Cópia interrompida (ex.: requisição cancelada): o arquivo pela metade não fica órfão na pasta.
            File.Delete(path);

            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        try
        {
            Stream fileStream = new FileStream(
                ResolveContainedPath(storageKey),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            return Task.FromResult<Stream?>(fileStream);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            // Arquivo apagado do disco com a linha ainda no banco: quem chama decide a resposta.
            return Task.FromResult<Stream?>(null);
        }
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        // File.Delete não falha quando o arquivo não existe.
        File.Delete(ResolveContainedPath(storageKey));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Monta o caminho do arquivo e recusa chaves que apontem para fora da pasta de anexos (ex.: "..\").
    /// </summary>
    private string ResolveContainedPath(string storageKey)
    {
        string fullPath = Path.GetFullPath(Path.Combine(_rootPath, storageKey));

        if (!fullPath.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("O caminho resolvido está fora do diretório de armazenamento.");
        }

        return fullPath;
    }

    private static string SanitizeExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension) || extension.Length > 10)
        {
            return string.Empty;
        }

        return extension.All(c => char.IsLetterOrDigit(c) || c == '.') ? extension : string.Empty;
    }
}
