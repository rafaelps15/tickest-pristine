using TickestPristine.Application.Abstractions.Storage;
using Microsoft.Extensions.Options;

namespace TickestPristine.Infrastructure.Storage;

/// <summary>
/// Guarda os arquivos dos anexos numa pasta do servidor. A chave de cada arquivo é um Guid com a extensão original.
/// </summary>
internal sealed class LocalFileStorage(IOptions<FileStorageOptions> options) : IFileStorage
{
    private const int BufferSize = 81920;

    private readonly string _rootPath = Path.GetFullPath(options.Value.RootPath);

    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_rootPath);

        string storageKey = $"{Guid.NewGuid():N}{SanitizeExtension(Path.GetExtension(fileName))}";

        await using var fileStream = new FileStream(
            ResolveContainedPath(storageKey),
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous);

        await content.CopyToAsync(fileStream, cancellationToken);

        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        Stream fileStream = new FileStream(
            ResolveContainedPath(storageKey),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return Task.FromResult(fileStream);
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
