namespace TickestPristine.IntegrationTests.Evidence.Ct26;

/// <summary>
/// Teste de evidência sob demanda: só roda com a variável de ambiente <see cref="VariableName"/> igual a <c>1</c>; sem
/// ela, aparece como ignorado, com o motivo. Ele mostra o comportamento atual da biblioteca ou da API, e não um requisito
/// da solução, por isso fica fora da suíte permanente.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class EvidenceFactAttribute : FactAttribute
{
    public const string VariableName = "TICKEST_EVIDENCIA_CT26";

    public EvidenceFactAttribute()
    {
        if (!IsEnabled)
        {
            Skip = $"Evidência sob demanda: rode com {VariableName}=1.";
        }
    }

    public static bool IsEnabled => Environment.GetEnvironmentVariable(VariableName) == "1";
}
