namespace TickestPristine.SharedKernel;

/// <summary>
/// Marca as entidades cujas inclusões, alterações e exclusões vão para o log de auditoria.
/// </summary>
#pragma warning disable CA1040, S4023 // Interface de marcação: o interceptor de auditoria filtra as entidades por ela.
public interface IAuditable;
#pragma warning restore CA1040, S4023
