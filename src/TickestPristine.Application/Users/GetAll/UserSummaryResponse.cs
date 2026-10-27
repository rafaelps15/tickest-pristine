namespace TickestPristine.Application.Users.GetAll;

public sealed record UserSummaryResponse
{
    public Guid Id { get; init; }
    public string Email { get; init; }
    public string FirstName { get; init; }
    public string LastName { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? DeactivatedAtUtc { get; init; }
    public Guid? SectorId { get; init; }
    public string? SectorName { get; init; }
    public string? DepartmentName { get; init; }
    public bool? SectorIsActive { get; init; }
    public bool PendingActivation { get; init; }
    public DateTime? InvitationExpiresAtUtc { get; init; }
    public List<RoleSummaryResponse> Roles { get; init; } = [];
}

public sealed record RoleSummaryResponse
{
    public string Name { get; init; }
}
