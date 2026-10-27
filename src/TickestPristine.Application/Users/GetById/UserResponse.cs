namespace TickestPristine.Application.Users.GetById;

public sealed record UserResponse
{
    public Guid Id { get; init; }

    public string Email { get; init; }

    public string FirstName { get; init; }

    public string LastName { get; init; }

    public Guid? SectorId { get; init; }

    public string? SectorName { get; init; }

    public string? DepartmentName { get; init; }

    public bool? SectorIsActive { get; init; }

    public bool PendingActivation { get; init; }

    public DateTime? InvitationExpiresAtUtc { get; init; }
}
