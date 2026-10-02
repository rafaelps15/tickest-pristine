namespace TickestPristine.Application.Users.GetCurrent;

public sealed record CurrentUserResponse
{
    public Guid Id { get; init; }
    public string Email { get; init; }
    public string FirstName { get; init; }
    public string LastName { get; init; }
    public List<CurrentUserRoleResponse> Roles { get; init; } = [];
    public List<string> Permissions { get; init; } = [];
}

public sealed record CurrentUserRoleResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; }
}
