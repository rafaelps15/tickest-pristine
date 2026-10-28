using TickestPristine.Application.TicketHistories;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.UnitTests.TicketHistories;

public sealed class TicketHistoryDescriptionsTests
{
    [Theory]
    [InlineData(TicketHistoryAction.Created, "Chamado criado")]
    [InlineData(TicketHistoryAction.DescriptionChanged, "Descrição alterada")]
    [InlineData(TicketHistoryAction.Reopened, "Chamado reaberto")]
    [InlineData(TicketHistoryAction.Deleted, "Chamado excluído")]
    [InlineData(TicketHistoryAction.MessageAdded, "Nova mensagem adicionada ao chamado")]
    [InlineData(TicketHistoryAction.MessageEdited, "Mensagem editada")]
    [InlineData(TicketHistoryAction.MessageRemoved, "Mensagem removida")]
    [InlineData(TicketHistoryAction.AttachmentRemoved, "Anexo removido")]
    public void For_Should_ReturnFixedText_WhenActionHasNoValues(TicketHistoryAction action, string expected)
    {
        // Arrange
        const string? noValue = null;

        // Act
        string description = TicketHistoryDescriptions.For(action, noValue, noValue);

        // Assert
        description.ShouldBe(expected);
    }

    [Fact]
    public void For_Should_DescribeStatusNames_WhenStatusChanged()
    {
        // Arrange
        string oldValue = nameof(TicketStatus.Open);
        string newValue = nameof(TicketStatus.InProgress);

        // Act
        string description = TicketHistoryDescriptions.For(TicketHistoryAction.StatusChanged, oldValue, newValue);

        // Assert
        description.ShouldBe("Status alterado de Aberto para Em andamento");
    }

    [Fact]
    public void For_Should_KeepValue_WhenStatusIsUnknown()
    {
        // Arrange
        const string unknownStatus = "Archived";

        // Act
        string description = TicketHistoryDescriptions.For(
            TicketHistoryAction.StatusChanged,
            unknownStatus,
            nameof(TicketStatus.Open));

        // Assert
        description.ShouldBe("Status alterado de Archived para Aberto");
    }

    [Fact]
    public void For_Should_IncludeFileName_WhenAttachmentAdded()
    {
        // Arrange
        const string fileName = "comprovante.pdf";

        // Act
        string description = TicketHistoryDescriptions.For(TicketHistoryAction.AttachmentAdded, null, fileName);

        // Assert
        description.ShouldBe("Anexo 'comprovante.pdf' adicionado");
    }

    [Fact]
    public void For_Should_NameOldAndNewAssignee_WhenReassigned()
    {
        // Arrange
        string anaId = Guid.NewGuid().ToString();
        string betoId = Guid.NewGuid().ToString();

        // Act
        string description = TicketHistoryDescriptions.For(
            TicketHistoryAction.Reassigned,
            anaId,
            betoId,
            "Ana Souza",
            "Beto Lima");

        // Assert
        description.ShouldBe("Responsável alterado de Ana Souza para Beto Lima");
    }

    [Fact]
    public void For_Should_NameNewAssignee_WhenAssignedOrReassignedWithoutPreviousAssignee()
    {
        // Arrange
        string anaId = Guid.NewGuid().ToString();

        // Act
        string assigned = TicketHistoryDescriptions.For(TicketHistoryAction.Assigned, null, anaId, newUserName: "Ana Souza");
        string legacyFirstAssignment = TicketHistoryDescriptions.For(
            TicketHistoryAction.Reassigned,
            null,
            anaId,
            newUserName: "Ana Souza");

        // Assert
        assigned.ShouldBe("Responsável definido: Ana Souza");
        legacyFirstAssignment.ShouldBe("Responsável definido: Ana Souza");
    }

    [Fact]
    public void For_Should_NameRemovedAssignee_WhenUnassigned()
    {
        // Arrange
        string anaId = Guid.NewGuid().ToString();

        // Act
        string description = TicketHistoryDescriptions.For(TicketHistoryAction.Unassigned, anaId, null, oldUserName: "Ana Souza");

        // Assert
        description.ShouldBe("Responsável removido: Ana Souza");
    }

    [Fact]
    public void For_Should_ShowRemovedUser_WhenAssigneeHasNoName()
    {
        // Arrange
        string unknownUserId = Guid.NewGuid().ToString();

        // Act
        string unassigned = TicketHistoryDescriptions.For(TicketHistoryAction.Unassigned, unknownUserId, null);
        string reassigned = TicketHistoryDescriptions.For(
            TicketHistoryAction.Reassigned,
            unknownUserId,
            Guid.NewGuid().ToString(),
            newUserName: "Beto Lima");

        // Assert
        unassigned.ShouldBe("Responsável removido: usuário removido");
        reassigned.ShouldBe("Responsável alterado de usuário removido para Beto Lima");
    }

    [Fact]
    public void For_Should_NameDeactivatedAssignee_WhenUnassignedOnUserDeactivation()
    {
        // Arrange
        string anaId = Guid.NewGuid().ToString();

        // Act
        string named = TicketHistoryDescriptions.For(
            TicketHistoryAction.UnassignedOnUserDeactivation,
            anaId,
            null,
            oldUserName: "Ana Souza");
        string unnamed = TicketHistoryDescriptions.For(TicketHistoryAction.UnassignedOnUserDeactivation, anaId, null);

        // Assert
        named.ShouldBe("Responsável removido automaticamente (usuário desativado): Ana Souza");
        unnamed.ShouldBe("Responsável removido automaticamente (usuário desativado): usuário removido");
    }
}
