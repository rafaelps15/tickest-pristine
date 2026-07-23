namespace TickestPristine.Application.Users.GetAll;

public sealed class UserSummaryResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
}
