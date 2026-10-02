namespace TickestPristine.Application.Users.GetAll;

public sealed record UserSummaryResponse
{
    public Guid Id { get; init; }
    public string Email { get; init; }
    public string FirstName { get; init; }
    public string LastName { get; init; }
    public List<RoleSummaryResponse> Roles { get; init; } = [];
}

public sealed record RoleSummaryResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; }
}
