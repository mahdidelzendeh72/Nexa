using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nexa.Application.Common;
using Nexa.Domain.Models;
using Nexa.Infrastructure.Identity;
using Nexa.Infrastructure.Persistence;

namespace Nexa.Infrastructure.Persistence;

public sealed class NexaDataSeeder(
    NexaDbContext db,
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    IConfiguration configuration,
    ILogger<NexaDataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        foreach (var roleName in NexaRoles.All)
        {
            if (!await roles.RoleExistsAsync(roleName))
            {
                var created = await roles.CreateAsync(new IdentityRole<Guid>(roleName));
                if (!created.Succeeded)
                {
                    throw new InvalidOperationException($"Failed to create role '{roleName}'.");
                }
            }
        }

        var adminEmail = configuration["Seed:AdminEmail"] ?? "admin@nexa.local";
        var adminPassword = configuration["Seed:AdminPassword"] ?? "ChangeMe!Nexa1";
        var admin = await users.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                Id = Guid.CreateVersion7(),
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                DisplayName = "Nexa Admin"
            };

            var result = await users.CreateAsync(admin, adminPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
            }

            await users.AddToRolesAsync(admin, [NexaRoles.Admin, NexaRoles.User]);
            logger.LogInformation("Seeded admin user {Email}", adminEmail);
        }

        if (!await db.ModelProviders.AnyAsync(cancellationToken))
        {
            var ollama = ModelProvider.Create("Ollama", ModelProviderKind.Ollama);
            var openAi = ModelProvider.Create("OpenAI", ModelProviderKind.OpenAI);
            db.ModelProviders.AddRange(ollama, openAi);
            db.ModelProfiles.Add(ModelProfile.Create(ollama.Id, "Local Llama", "gpt-oss:20b", 0.7, 2048));
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded default model providers");
        }
    }
}
