namespace TickestPristine.SharedKernel;

public sealed record ValidationError : Error
{
    public ValidationError(Error[] errors)
        : base(
            "Validation.General",
            "Um ou mais erros de validação ocorreram",
            ErrorType.Validation)
    {
        Errors = errors;
    }

    public Error[] Errors { get; }
}
