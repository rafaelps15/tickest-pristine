using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Infrastructure.Authorization;

namespace TickestPristine.IntegrationTests.Authorization;

public sealed class ActiveUserAuthorizationHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_Succeed_WhenUserIsActive()
    {
        // Arrange
        var provider = new RecordingPermissionProvider([], isActive: true);
        var handler = new ActiveUserAuthorizationHandler(provider);
        AuthorizationHandlerContext context = CreateContext(AuthenticatedUser(), resource: null);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_Should_NotSucceed_WhenUserIsInactive()
    {
        // Arrange
        var provider = new RecordingPermissionProvider([], isActive: false);
        var handler = new ActiveUserAuthorizationHandler(provider);
        AuthorizationHandlerContext context = CreateContext(AuthenticatedUser(), resource: null);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeFalse();
        provider.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task HandleAsync_Should_NotQueryProvider_WhenUserIsNotAuthenticated()
    {
        // Arrange
        var provider = new RecordingPermissionProvider([], isActive: true);
        var handler = new ActiveUserAuthorizationHandler(provider);
        AuthorizationHandlerContext context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity()), resource: null);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeFalse();
        provider.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task HandleRequirementAsync_Should_PassRequestCancellation_WhenProviderIsCalled()
    {
        // Arrange
        using var requestAborted = new CancellationTokenSource();
        await requestAborted.CancelAsync();

        var provider = new RecordingPermissionProvider([], isActive: true);
        var handler = new ActiveUserAuthorizationHandler(provider);
        var httpContext = new DefaultHttpContext { RequestAborted = requestAborted.Token };
        AuthorizationHandlerContext context = CreateContext(AuthenticatedUser(), httpContext);

        // Act
        await handler.HandleAsync(context);

        // Assert
        provider.ReceivedToken.ShouldBe(requestAborted.Token);
    }

    [Fact]
    public void Handler_Should_UseRequestProvider_InsteadOfServiceScopeFactory()
    {
        // Arrange
        Type handlerType = typeof(ActiveUserAuthorizationHandler);

        // Act
        Type[] dependencies = [.. handlerType.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)];

        // Assert
        dependencies.ShouldNotContain(typeof(IServiceScopeFactory));
        dependencies.ShouldContain(typeof(IPermissionProvider));
    }

    private static AuthorizationHandlerContext CreateContext(ClaimsPrincipal user, object? resource) =>
        new([new ActiveUserRequirement()], user, resource);

    private static ClaimsPrincipal AuthenticatedUser() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "Test"));
}
