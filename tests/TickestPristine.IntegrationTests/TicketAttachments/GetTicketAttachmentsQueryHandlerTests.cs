using TickestPristine.Application.Tickets;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.TicketAttachments.GetAll;
using TickestPristine.IntegrationTests.Abstractions;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using NSubstitute;
using TickestPristine.SharedKernel;

namespace TickestPristine.IntegrationTests.TicketAttachments;

public sealed class GetTicketAttachmentsQueryHandlerTests(IntegrationTestWebAppFactory factory) : BaseQueryHandlerTest(factory)
{
    private static readonly Guid CreatorId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new GetTicketAttachmentsQueryHandler(DbConnectionFactory, userContext, new TicketViewAccess(permissionProvider));
        var query = new GetTicketAttachmentsQuery(Guid.NewGuid(), new CursorPageRequest(null, null, 20));

        // Act
        Result<CursorPagedResponse<TicketAttachmentResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotFound(query.TicketId));
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerIsNotParticipantAndLacksManagePermission()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketWithAttachmentAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        var outsiderId = Guid.NewGuid();
        userContext.UserId.Returns(outsiderId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(outsiderId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new GetTicketAttachmentsQueryHandler(DbConnectionFactory, userContext, new TicketViewAccess(permissionProvider));
        var query = new GetTicketAttachmentsQuery(ticketId, new CursorPageRequest(null, null, 20));

        // Act
        Result<CursorPagedResponse<TicketAttachmentResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_ReturnAttachments_WhenCallerIsParticipant()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketWithAttachmentAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new GetTicketAttachmentsQueryHandler(DbConnectionFactory, userContext, new TicketViewAccess(permissionProvider));
        var query = new GetTicketAttachmentsQuery(ticketId, new CursorPageRequest(null, null, 20));

        // Act
        Result<CursorPagedResponse<TicketAttachmentResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldHaveSingleItem();
        result.Value.Items[0].FileName.ShouldBe("report.pdf");
    }

    [Fact]
    public async Task Handle_Should_ReturnAttachments_WhenCallerHasManagePermission()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketWithAttachmentAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        var managerId = Guid.NewGuid();
        userContext.UserId.Returns(managerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(managerId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new GetTicketAttachmentsQueryHandler(DbConnectionFactory, userContext, new TicketViewAccess(permissionProvider));
        var query = new GetTicketAttachmentsQuery(ticketId, new CursorPageRequest(null, null, 20));

        // Act
        Result<CursorPagedResponse<TicketAttachmentResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldHaveSingleItem();
    }

    private static async Task<Guid> SeedTicketWithAttachmentAsync(ApplicationDbContext context)
    {
        Ticket ticket = QueryTicketTestData.Create(requesterId: CreatorId, description: "Original description");
        context.Tickets.Add(ticket);

        TicketAttachment attachment = QueryTicketTestData.CreateAttachment(ticket.Id, CreatorId, DateTime.UtcNow);
        context.TicketAttachments.Add(attachment);

        await context.SaveChangesAsync();

        return ticket.Id;
    }
}
