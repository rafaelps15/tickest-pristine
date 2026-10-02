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
        // Arrange
        var command = new CreateTicketCommand
        {
            Title = string.Empty,
            Description = "Some description",
            Priority = TicketPriority.Low,
            SectorId = Guid.NewGuid()
        };

        // Act
        TestValidationResult<CreateTicketCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Title);
    }

    [Fact]
    public void CreateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new CreateTicketCommand
        {
            Title = "Printer is broken",
            Description = "The printer on the 3rd floor is not working",
            Priority = TicketPriority.Medium,
            SectorId = Guid.NewGuid()
        };

        // Act
        TestValidationResult<CreateTicketCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateValidator_Should_HaveError_WhenDescriptionIsTooShort()
    {
        // Arrange
        var command = new UpdateTicketCommand { TicketId = Guid.NewGuid(), Description = "short", Status = TicketStatus.Open };

        // Act
        TestValidationResult<UpdateTicketCommand> result = _updateValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void UpdateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new UpdateTicketCommand { TicketId = Guid.NewGuid(), Description = "Updated ticket description", Status = TicketStatus.InProgress };

        // Act
        TestValidationResult<UpdateTicketCommand> result = _updateValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DeleteValidator_Should_HaveError_WhenTicketIdIsEmpty()
    {
        // Arrange
        var command = new DeleteTicketCommand(Guid.Empty);

        // Act
        TestValidationResult<DeleteTicketCommand> result = _deleteValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.TicketId);
    }

    [Fact]
    public void DeleteValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new DeleteTicketCommand(Guid.NewGuid());

        // Act
        TestValidationResult<DeleteTicketCommand> result = _deleteValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ReopenValidator_Should_HaveError_WhenTicketIdIsEmpty()
    {
        // Arrange
        var command = new ReopenTicketCommand(Guid.Empty);

        // Act
        TestValidationResult<ReopenTicketCommand> result = _reopenValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.TicketId);
    }

    [Fact]
    public void ReopenValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new ReopenTicketCommand(Guid.NewGuid());

        // Act
        TestValidationResult<ReopenTicketCommand> result = _reopenValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenTitleExceedsMaxLength()
    {
        // Arrange
        var command = new CreateTicketCommand
        {
            Title = new string('a', 201),
            Description = "Some description",
            Priority = TicketPriority.Low,
            SectorId = Guid.NewGuid()
        };

        // Act
        TestValidationResult<CreateTicketCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Title);
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenDescriptionIsEmpty()
    {
        // Arrange
        var command = new CreateTicketCommand
        {
            Title = "Printer is broken",
            Description = string.Empty,
            Priority = TicketPriority.Low,
            SectorId = Guid.NewGuid()
        };

        // Act
        TestValidationResult<CreateTicketCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenDescriptionIsTooShort()
    {
        // Arrange
        var command = new CreateTicketCommand
        {
            Title = "Printer is broken",
            Description = "short",
            Priority = TicketPriority.Low,
            SectorId = Guid.NewGuid()
        };

        // Act
        TestValidationResult<CreateTicketCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenDescriptionExceedsMaxLength()
    {
        // Arrange
        var command = new CreateTicketCommand
        {
            Title = "Printer is broken",
            Description = new string('a', 501),
            Priority = TicketPriority.Low,
            SectorId = Guid.NewGuid()
        };

        // Act
        TestValidationResult<CreateTicketCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenPriorityIsNotInEnum()
    {
        // Arrange
        var command = new CreateTicketCommand
        {
            Title = "Printer is broken",
            Description = "Some description",
            Priority = (TicketPriority)99,
            SectorId = Guid.NewGuid()
        };

        // Act
        TestValidationResult<CreateTicketCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Priority);
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenSectorIdIsEmpty()
    {
        // Arrange
        var command = new CreateTicketCommand
        {
            Title = "Printer is broken",
            Description = "Some description",
            Priority = TicketPriority.Low,
            SectorId = Guid.Empty
        };

        // Act
        TestValidationResult<CreateTicketCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.SectorId);
    }

    [Fact]
    public void UpdateValidator_Should_HaveError_WhenTicketIdIsEmpty()
    {
        // Arrange
        var command = new UpdateTicketCommand { TicketId = Guid.Empty, Description = "Updated ticket description", Status = TicketStatus.InProgress };

        // Act
        TestValidationResult<UpdateTicketCommand> result = _updateValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.TicketId);
    }

    [Fact]
    public void UpdateValidator_Should_HaveError_WhenDescriptionIsEmpty()
    {
        // Arrange
        var command = new UpdateTicketCommand { TicketId = Guid.NewGuid(), Description = string.Empty, Status = TicketStatus.InProgress };

        // Act
        TestValidationResult<UpdateTicketCommand> result = _updateValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void UpdateValidator_Should_HaveError_WhenDescriptionIsTooLong()
    {
        // Arrange
        var command = new UpdateTicketCommand { TicketId = Guid.NewGuid(), Description = new string('a', 501), Status = TicketStatus.InProgress };

        // Act
        TestValidationResult<UpdateTicketCommand> result = _updateValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void UpdateValidator_Should_HaveError_WhenStatusIsNotInEnum()
    {
        // Arrange
        var command = new UpdateTicketCommand { TicketId = Guid.NewGuid(), Description = "Updated ticket description", Status = (TicketStatus)99 };

        // Act
        TestValidationResult<UpdateTicketCommand> result = _updateValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Status);
    }
}
