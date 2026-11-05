using TickestPristine.SharedKernel;

namespace TickestPristine.ArchitectureTests.Conventions.Fixtures;

// Tipo de exemplo do teste de nomes de segredo da auditoria: nunca é persistido nem usado fora dos testes.

public sealed class AuditableFixtureWithSecret : IAuditable
{
    public string Name { get; set; } = string.Empty;

    public string ApiTokenHash { get; set; } = string.Empty;
}
