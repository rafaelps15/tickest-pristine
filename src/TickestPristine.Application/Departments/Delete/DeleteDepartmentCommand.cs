using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Departments.Delete;

public sealed record DeleteDepartmentCommand(Guid DepartmentId) : ICommand;
