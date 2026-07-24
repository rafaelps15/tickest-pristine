using TickestPristine.Application.Abstractions.Storage;
using Microsoft.Extensions.Configuration;

namespace TickestPristine.Infrastructure.Storage;

internal sealed class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;

    public LocalFileStorage(IConfiguration configuration)
    {
        string? configuredPath = configuration["FileStorage:RootPath"];

        _rootPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "attachments")
            : configuredPath;

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken)
    {
        string storageKey = $"{Guid.NewGuid():N}{SanitizeExtension(Path.GetExtension(fileName))}";
        string fullPath = Path.Combine(_rootPath, storageKey);

        await using FileStream fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);

        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        string fullPath = ResolveContainedPath(storageKey);

        return Task.FromResult<Stream>(File.OpenRead(fullPath));
    }

    private string ResolveContainedPath(string storageKey)
    {
        string rootFullPath = Path.GetFullPath(_rootPath);
        string fullPath = Path.GetFullPath(Path.Combine(rootFullPath, storageKey));

        if (!fullPath.StartsWith(rootFullPath, StringComparison.Ordinal))
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
