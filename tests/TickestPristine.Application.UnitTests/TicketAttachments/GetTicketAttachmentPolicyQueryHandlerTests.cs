using TickestPristine.Application.TicketAttachments;
using TickestPristine.Application.TicketAttachments.GetPolicy;

namespace TickestPristine.Application.UnitTests.TicketAttachments;

public sealed class GetTicketAttachmentPolicyQueryHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnLimitsAndAllowedTypesUsedByValidation_WhenPolicyIsRequested()
    {
        // Arrange
        var handler = new GetTicketAttachmentPolicyQueryHandler();

        // Act
        Result<TicketAttachmentPolicyResponse> result = await handler.Handle(
            new GetTicketAttachmentPolicyQuery(),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.MaxFileSizeBytes.ShouldBe(10_485_760);
        result.Value.MaxAttachmentsPerTicket.ShouldBe(20);
        result.Value.MaxTotalBytesPerTicket.ShouldBe(104_857_600);
        result.Value.AllowedTypes.Count.ShouldBe(11);
        result.Value.AllowedTypes.Single(t => t.ContentType == "image/jpeg").Extensions.ShouldBe([".jpg", ".jpeg"]);

        // Todo tipo informado é aceito pela validação do envio, com cada uma das extensões informadas.
        result.Value.AllowedTypes.ShouldAllBe(type =>
            TicketAttachmentLimits.IsAllowedContentType(type.ContentType) &&
            type.Extensions.All(extension =>
                TicketAttachmentLimits.HasExtensionOfContentType($"arquivo{extension}", type.ContentType)));
    }
}
