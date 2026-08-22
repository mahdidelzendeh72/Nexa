using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexa.Application.Abstractions;
using Nexa.Application.Common;
using Nexa.Infrastructure.Ai;
using Nexa.Infrastructure.Identity;
using Nexa.Infrastructure.Persistence;
using Nexa.Infrastructure.Persistence.Repositories;

namespace Nexa.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNexaInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ModelConnectionsOptions>(configuration.GetSection(ModelConnectionsOptions.SectionName));

        var provider = configuration["Database:Provider"] ?? "PostgreSQL";
        var connectionString = configuration.GetConnectionString("Nexa")
            ?? throw new InvalidOperationException("Connection string 'Nexa' is not configured.");

        services.AddDbContext<NexaDbContext>(options =>
        {
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseNpgsql(connectionString);
            }
        });

        services
            .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequiredLength = 10;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
            })
            .AddEntityFrameworkStores<NexaDbContext>()
            .AddDefaultTokenProviders()
            .AddDefaultUI();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Identity/Account/Login";
            options.AccessDeniedPath = "/Identity/Account/AccessDenied";
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(12);
            options.Cookie.HttpOnly = true;
            options.Cookie.Name = "Nexa.Auth";
            options.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(NexaPolicies.CanView, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(NexaPolicies.CanChat, policy => policy.RequireRole(
                NexaRoles.Admin, NexaRoles.AgentAdmin, NexaRoles.Developer, NexaRoles.User))
            .AddPolicy(NexaPolicies.CanManageAgents, policy => policy.RequireRole(
                NexaRoles.Admin, NexaRoles.AgentAdmin, NexaRoles.Developer))
            .AddPolicy(NexaPolicies.CanManageModels, policy => policy.RequireRole(
                NexaRoles.Admin, NexaRoles.Developer));

        services.AddHttpContextAccessor();
        services.AddTransient<Microsoft.AspNetCore.Authentication.IClaimsTransformation, DefaultRoleClaimsTransformation>();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<NexaDbContext>());
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IModelCatalogRepository, ModelCatalogRepository>();
        services.AddScoped<IAgentRunRepository, AgentRunRepository>();
        services.AddScoped<IProviderConnectionResolver, ProviderConnectionResolver>();
        services.AddScoped<IChatClientFactory, OpenAiCompatibleChatClientFactory>();
        services.AddSingleton<BuiltInToolCatalog>();
        services.AddSingleton<IToolRegistry>(sp => sp.GetRequiredService<BuiltInToolCatalog>());
        services.AddScoped<IAgentRuntime, AgentFrameworkRuntime>();
        services.AddScoped<NexaDataSeeder>();
        return services;
    }
}
