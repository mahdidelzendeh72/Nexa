using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Nexa.Application.Agents;
using Nexa.Application.Conversations;
using Nexa.Application.Models;

namespace Nexa.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNexaApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<IModelProfileService, ModelProfileService>();
        services.AddScoped<IConversationService, ConversationService>();
        return services;
    }
}
