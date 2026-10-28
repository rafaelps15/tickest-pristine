namespace TickestPristine.Application.Users.GetStatusCounts;

/// <summary>
/// As três situações são disjuntas e somam <see cref="TotalCount"/>: <see cref="ActiveCount"/> são os ativos que já
/// definiram a senha, <see cref="PendingActivationCount"/> são os ativos ainda sem senha (convite pendente) e
/// <see cref="InactiveCount"/> são todos os desativados, com ou sem senha.
/// </summary>
public sealed class UserStatusCountsResponse
{
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public int InactiveCount { get; set; }
    public int PendingActivationCount { get; set; }
}
