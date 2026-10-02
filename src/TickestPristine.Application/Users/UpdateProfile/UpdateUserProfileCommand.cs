using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.UpdateProfile;

public sealed record UpdateUserProfileCommand(Guid UserId, string FirstName, string LastName) : ICommand;
