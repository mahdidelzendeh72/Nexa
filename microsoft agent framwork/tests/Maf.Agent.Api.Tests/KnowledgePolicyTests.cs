using Maf.Agent.Api.Knowledge.Application;
using Maf.Agent.Api.Knowledge.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Maf.Agent.Api.Tests;

public sealed class KnowledgePolicyTests
{
    [Fact]
    public async Task Reader_can_read_but_cannot_manage()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddKnowledgeApi();
        await using ServiceProvider provider = services.BuildServiceProvider();
        AuthorizationOptions options =
            provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        AuthorizationPolicy read = options.GetPolicy(KnowledgePolicies.Read)!;
        AuthorizationPolicy manage = options.GetPolicy(KnowledgePolicies.Manage)!;

        Assert.True(await AuthorizeAsync(read, KnowledgeRoles.Reader));
        Assert.False(await AuthorizeAsync(manage, KnowledgeRoles.Reader));
        Assert.True(await AuthorizeAsync(manage, KnowledgeRoles.Manager));
    }

    private static async Task<bool> AuthorizeAsync(
        AuthorizationPolicy policy,
        string role)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddAuthorization();
        await using ServiceProvider provider = services.BuildServiceProvider();
        IAuthorizationService authorization =
            provider.GetRequiredService<IAuthorizationService>();
        System.Security.Claims.ClaimsPrincipal user = new(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(
                    System.Security.Claims.ClaimTypes.Role,
                    role)],
                "test"));

        AuthorizationResult result =
            await authorization.AuthorizeAsync(user, resource: null, policy);
        return result.Succeeded;
    }
}
