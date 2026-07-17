using TickestPristine.Application.Tickets.Create;
using TickestPristine.Application.Tickets.Update;
using TickestPristine.Domain.Tickets;
using FluentValidation.TestHelper;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class TicketValidatorsTests
{
    private readonly CreateTicketCommandValidator _createValidator = new();
    private readonly UpdateTicketCommandValidator _updateValidator = new();

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
}
