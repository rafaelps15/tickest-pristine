using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Departments.Deactivate;

public sealed record DeactivateDepartmentCommand(Guid DepartmentId) : ICommand;
