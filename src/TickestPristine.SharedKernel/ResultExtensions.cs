namespace TickestPristine.SharedKernel;

public static class ResultExtensions
{
    /// <summary>
    /// Mantém o sucesso quando o valor atende ao predicado, ou devolve o erro informado; uma falha anterior segue
    /// adiante sem avaliar o predicado.
    /// </summary>
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Error error)
    {
        if (result.IsFailure)
        {
            return result;
        }

        return predicate(result.Value) ? result : Result.Failure<T>(error);
    }

    /// <summary>
    /// Transforma o valor do sucesso; a falha segue adiante com o mesmo erro, sem chamar a função.
    /// </summary>
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> func) =>
        result.IsSuccess ? func(result.Value) : Result.Failure<TOut>(result.Error);
}
