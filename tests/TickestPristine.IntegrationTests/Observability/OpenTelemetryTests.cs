using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TickestPristine.Application.Abstractions.Telemetry;

namespace TickestPristine.IntegrationTests.Observability;

public sealed class OpenTelemetryTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public void Tracing_Should_ListenToApplicationSourceAndPublishServiceVersion_When_ApiStarts()
    {
        // Arrange
        string expectedVersion = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        TracerProvider tracerProvider = Services.GetRequiredService<TracerProvider>();

        // Act
        Resource resource = tracerProvider.GetResource();
        using var applicationSource = new ActivitySource(ApplicationTelemetry.Name);

        // Assert: nenhum outro ouvinte de spans existe no teste, então quem escuta a fonte é o OpenTelemetry da API
        resource.Attributes.ShouldContain(attribute =>
            attribute.Key == "service.version" && Equals(attribute.Value, expectedVersion));
        applicationSource.HasListeners().ShouldBeTrue();
    }
}
