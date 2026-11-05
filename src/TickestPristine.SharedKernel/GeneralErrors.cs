namespace TickestPristine.SharedKernel;

public static class GeneralErrors
{
    // O mesmo detalhe da resposta do GlobalExceptionHandler ao conflito de concorrência do EF.
    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "General.ConcurrencyConflict",
        "Os dados foram alterados por outra operação ao mesmo tempo. Atualize a tela e tente novamente.");
}
