using TickestPristine.Application.TicketHistories;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.UnitTests.TicketHistories;

public sealed class TicketHistoryDescriptionsTests
{
    [Theory]
    [InlineData(TicketHistoryAction.Created, "Chamado criado")]
    [InlineData(TicketHistoryAction.Reassigned, "Responsável alterado")]
    [InlineData(TicketHistoryAction.Unassigned, "Responsável removido")]
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
}
