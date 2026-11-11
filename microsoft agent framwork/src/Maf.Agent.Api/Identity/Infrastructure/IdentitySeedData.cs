using Maf.Agent.Api.Identity.Application;
using Maf.Agent.Api.Identity.Domain;
using Maf.Agent.Api.Knowledge.Application;
using Maf.Agent.Api.Mcp.Application;
using Maf.Agent.Api.Models.Application;
using Microsoft.AspNetCore.Identity;

namespace Maf.Agent.Api.Identity.Infrastructure;

public static class IdentitySeedData
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger)
    {
        string? email = configuration["SeedUser:Email"];
        string? password = configuration["SeedUser:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Development seed user was not created because SeedUser credentials are not configured.");
            return;
        }

        UserManager<ApplicationUser> userManager =
            services.GetRequiredService<UserManager<ApplicationUser>>();
        RoleManager<IdentityRole> roleManager =
            services.GetRequiredService<RoleManager<IdentityRole>>();

        string[] roles =
        [
            KnowledgeRoles.Reader,
            KnowledgeRoles.Manager,
            McpRoles.Manager,
            ModelRoles.Manager,
            AccountRoles.Administrator,
        ];

        foreach (string roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                IdentityResult roleResult =
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                EnsureSucceeded(roleResult, $"create role '{roleName}'");
            }
        }

        ApplicationUser? user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Email = email,
                UserName = email,
                EmailConfirmed = true,
            };

            IdentityResult createResult =
                await userManager.CreateAsync(user, password);
            EnsureSucceeded(createResult, "create the development user");
            logger.LogInformation("Development seed user {Email} was created.", email);
        }

        IList<string> currentRoles = await userManager.GetRolesAsync(user);
        string[] missingRoles = roles
            .Except(currentRoles, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (missingRoles.Length > 0)
        {
            IdentityResult addRolesResult =
                await userManager.AddToRolesAsync(user, missingRoles);
            EnsureSucceeded(addRolesResult, "assign development roles");
        }

        logger.LogInformation(
            "Development seed user {Email} has the development roles.",
            email);
    }

    private static void EnsureSucceeded(
        IdentityResult result,
        string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        string errors = string.Join(
            "; ",
            result.Errors.Select(error => $"{error.Code}: {error.Description}"));
        throw new InvalidOperationException($"Could not {operation}. {errors}");
    }
}
