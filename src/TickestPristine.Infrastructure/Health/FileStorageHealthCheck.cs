using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TickestPristine.Infrastructure.Storage;

namespace TickestPristine.Infrastructure.Health;

// Grava e apaga um arquivo de teste na pasta dos anexos, a mesma do LocalFileStorage: a pasta que não pode ser criada
// ou não aceita escrita aparece aqui, antes do primeiro envio de anexo.
internal sealed class FileStorageHealthCheck(IOptions<FileStorageOptions> options) : IHealthCheck
{
    private static readonly byte[] ProbeContent = [0];

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        string rootPath = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(rootPath);

        string probePath = Path.Combine(rootPath, $"health-{Guid.NewGuid():N}.tmp");

        // DeleteOnClose: o arquivo some ao fechar, mesmo se a escrita falhar no meio.
        await using (var probe = new FileStream(
            probePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous))
        {
            await probe.WriteAsync(ProbeContent, cancellationToken);
        }

        return HealthCheckResult.Healthy();
    }
}
