using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed class User : Entity
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Code { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? DeactivatedAtUtc { get; set; }
}
