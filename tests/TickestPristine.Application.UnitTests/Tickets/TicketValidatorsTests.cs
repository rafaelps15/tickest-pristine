using TickestPristine.Application.Tickets.Create;
using TickestPristine.Application.Tickets.Delete;
using TickestPristine.Application.Tickets.Reopen;
using TickestPristine.Application.Tickets.Update;
using TickestPristine.Domain.Tickets;
using FluentValidation.TestHelper;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class TicketValidatorsTests
{
    private readonly CreateTicketCommandValidator _createValidator = new();
    private readonly UpdateTicketCommandValidator _updateValidator = new();
    private readonly DeleteTicketCommandValidator _deleteValidator = new();
    private readonly ReopenTicketCommandValidator _reopenValidator = new();

    [Fact]
    public void CreateValidator_Should_HaveError_WhenTitleIsEmpty()
    {
        var command = new CreateTicketCommand
        {
            Title = string.Empty,
            Description = "Some description",
            Priority = TicketPriority.Low,
            SectorId = Guid.NewGuid()
        };

        TestValidationResult<CreateTicketCommand> result = _createValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Title);
    }

    [Fact]
    public void CreateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        var command = new CreateTicketCommand
        {
            Title = "Printer is broken",
            Description = "The printer on the 3rd floor is not working",
            Priority = TicketPriority.Medium,
            SectorId = Guid.NewGuid()
        };

        TestValidationResult<CreateTicketCommand> result = _createValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateValidator_Should_HaveError_WhenDescriptionIsTooShort()
    {
        var command = new UpdateTicketCommand { TicketId = Guid.NewGuid(), Description = "short", Status = TicketStatus.Open };

        TestValidationResult<UpdateTicketCommand> result = _updateValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void UpdateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        var command = new UpdateTicketCommand { TicketId = Guid.NewGuid(), Description = "Updated ticket description", Status = TicketStatus.InProgress };

        TestValidationResult<UpdateTicketCommand> result = _updateValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DeleteValidator_Should_HaveError_WhenTicketIdIsEmpty()
    {
        var command = new DeleteTicketCommand(Guid.Empty);

        TestValidationResult<DeleteTicketCommand> result = _deleteValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.TicketId);
    }

    [Fact]
    public void DeleteValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        var command = new DeleteTicketCommand(Guid.NewGuid());

        TestValidationResult<DeleteTicketCommand> result = _deleteValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ReopenValidator_Should_HaveError_WhenTicketIdIsEmpty()
    {
        var command = new ReopenTicketCommand(Guid.Empty);

        TestValidationResult<ReopenTicketCommand> result = _reopenValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.TicketId);
    }

    [Fact]
    public void ReopenValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        var command = new ReopenTicketCommand(Guid.NewGuid());

        TestValidationResult<ReopenTicketCommand> result = _reopenValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
