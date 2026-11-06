using Microsoft.Extensions.Options;
using TickestPristine.Infrastructure.Storage;

namespace TickestPristine.IntegrationTests.Storage;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"tickest-storage-{Guid.NewGuid():N}");
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _storage = new LocalFileStorage(Options.Create(new FileStorageOptions { RootPath = _rootPath }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    [Fact]
    public void CreateKey_Should_KeepExtensionWithoutCreatingAnything_WhenCalled()
    {
        // Act
        string first = _storage.CreateKey("Relatorio.PDF");
        string second = _storage.CreateKey("Relatorio.PDF");

        // Assert
        Path.GetExtension(first).ShouldBe(".PDF");
        first.ShouldNotBe(second);
        Directory.Exists(_rootPath).ShouldBeFalse();
    }

    [Fact]
    public void CreateKey_Should_DropExtension_WhenExtensionHasUnsafeCharacters()
    {
        // Act
        string key = _storage.CreateKey("nota.p$f");

        // Assert
        Path.GetExtension(key).ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveAsync_Should_StoreContentReadableByKey_WhenKeyIsNew()
    {
        // Arrange
        string key = _storage.CreateKey("nota.txt");

        // Act
        await _storage.SaveAsync(new MemoryStream("Conteúdo do anexo"u8.ToArray()), key, CancellationToken.None);

        // Assert
        (await ReadAsync(key)).ShouldBe("Conteúdo do anexo");
    }

    [Fact]
    public async Task SaveAsync_Should_FailAndKeepExistingFile_WhenKeyIsTaken()
    {
        // Arrange
        string key = _storage.CreateKey("nota.txt");
        await _storage.SaveAsync(new MemoryStream("Primeiro"u8.ToArray()), key, CancellationToken.None);

        // Act
        Exception? exception = await Record.ExceptionAsync(() =>
            _storage.SaveAsync(new MemoryStream("Segundo"u8.ToArray()), key, CancellationToken.None));

        // Assert
        exception.ShouldBeOfType<IOException>();
        (await ReadAsync(key)).ShouldBe("Primeiro");
    }

    [Fact]
    public async Task SaveAsync_Should_LeaveNoFile_WhenCopyIsInterrupted()
    {
        // Arrange: a requisição cancelada interrompe a cópia depois de o arquivo ser criado
        string key = _storage.CreateKey("nota.txt");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        Exception? exception = await Record.ExceptionAsync(() =>
            _storage.SaveAsync(new MemoryStream("Conteúdo"u8.ToArray()), key, cancellation.Token));

        // Assert
        exception.ShouldBeAssignableTo<OperationCanceledException>();
        (await _storage.OpenReadAsync(key, CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_Should_RemoveFileAndIgnoreMissingKey_WhenCalledTwice()
    {
        // Arrange
        string key = _storage.CreateKey("nota.txt");
        await _storage.SaveAsync(new MemoryStream("Conteúdo"u8.ToArray()), key, CancellationToken.None);

        // Act
        await _storage.DeleteAsync(key, CancellationToken.None);
        Exception? exception = await Record.ExceptionAsync(() => _storage.DeleteAsync(key, CancellationToken.None));

        // Assert
        exception.ShouldBeNull();
        (await _storage.OpenReadAsync(key, CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task SaveAsync_Should_RefuseKey_WhenPathLeavesStorageFolder()
    {
        // Arrange
        string outsideName = $"fora-{Guid.NewGuid():N}.txt";

        // Act
        Exception? exception = await Record.ExceptionAsync(() =>
            _storage.SaveAsync(new MemoryStream("Conteúdo"u8.ToArray()), Path.Combine("..", outsideName), CancellationToken.None));

        // Assert
        exception.ShouldBeOfType<UnauthorizedAccessException>();
        File.Exists(Path.Combine(Path.GetDirectoryName(_rootPath)!, outsideName)).ShouldBeFalse();
    }

    private async Task<string> ReadAsync(string key)
    {
        await using Stream? content = await _storage.OpenReadAsync(key, CancellationToken.None);
        content.ShouldNotBeNull();

        using var reader = new StreamReader(content);

        return await reader.ReadToEndAsync();
    }
}
