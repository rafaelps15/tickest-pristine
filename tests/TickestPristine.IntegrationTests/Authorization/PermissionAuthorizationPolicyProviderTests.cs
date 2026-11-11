using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TickestPristine.Domain.Permissions;
using TickestPristine.Infrastructure.Authorization;

namespace TickestPristine.IntegrationTests.Authorization;

public sealed class PermissionAuthorizationPolicyProviderTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task GetPolicyAsync_Should_BuildPermissionPolicy_WhenNameIsNotRegistered()
    {
        // Arrange
        IAuthorizationPolicyProvider provider = Services.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        AuthorizationPolicy? policy = await provider.GetPolicyAsync(PermissionCodes.Tickets.Manage);

        // Assert
        provider.ShouldBeOfType<PermissionAuthorizationPolicyProvider>();
        policy.ShouldNotBeNull();
        policy.Requirements.ShouldHaveSingleItem().ShouldBeOfType<PermissionRequirement>()
            .Permission.ShouldBe(PermissionCodes.Tickets.Manage);
    }

    [Fact]
    public async Task GetPolicyAsync_Should_RequireOnlyAuthentication_WhenPolicyIsAuthenticatedUser()
    {
        // Arrange
        IAuthorizationPolicyProvider provider = Services.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        AuthorizationPolicy? policy = await provider.GetPolicyAsync(AuthorizationPolicies.AuthenticatedUser);

        // Assert
        policy.ShouldNotBeNull();
        policy.Requirements.ShouldHaveSingleItem().ShouldBeOfType<DenyAnonymousAuthorizationRequirement>();
    }

    [Fact]
    public async Task GetDefaultPolicyAsync_Should_RequireAuthenticatedActiveUser()
    {
        // Arrange
        IAuthorizationPolicyProvider provider = Services.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        AuthorizationPolicy policy = await provider.GetDefaultPolicyAsync();

        // Assert
        policy.Requirements.Count.ShouldBe(2);
        policy.Requirements.ShouldContain(requirement => requirement is DenyAnonymousAuthorizationRequirement);
        policy.Requirements.ShouldContain(requirement => requirement is ActiveUserRequirement);
    }

    [Fact]
    public async Task GetFallbackPolicyAsync_Should_RequireAuthenticatedActiveUser()
    {
        // Arrange
        IAuthorizationPolicyProvider provider = Services.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        AuthorizationPolicy? policy = await provider.GetFallbackPolicyAsync();

        // Assert
        policy.ShouldNotBeNull();
        policy.Requirements.Count.ShouldBe(2);
        policy.Requirements.ShouldContain(requirement => requirement is DenyAnonymousAuthorizationRequirement);
        policy.Requirements.ShouldContain(requirement => requirement is ActiveUserRequirement);
    }

    [Fact]
    public async Task GetPolicyAsync_Should_NotStorePermissionPolicy_WhenPolicyIsBuilt()
    {
        // Arrange
        IAuthorizationPolicyProvider provider = Services.GetRequiredService<IAuthorizationPolicyProvider>();
        AuthorizationOptions options = Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        string permission = $"teste:{Guid.NewGuid():N}";

        // Act
        AuthorizationPolicy? policy = await provider.GetPolicyAsync(permission);

        // Assert
        policy.ShouldNotBeNull();
        options.GetPolicy(permission).ShouldBeNull();
    }
}

