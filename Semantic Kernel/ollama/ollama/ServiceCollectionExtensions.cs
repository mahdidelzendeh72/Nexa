using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using ollama.Configuration;
using ollama.Middleware;
using ollama.Sessions;

namespace ollama;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOllamaChatInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<OllamaBootstrap>(_ => new OllamaBootstrap(configuration));
        services.AddHostedService<OllamaBootstrapHostedService>();
        services.AddSingleton(sp => sp.GetRequiredService<OllamaBootstrap>().Kernel);
        services.AddSingleton(sp => sp.GetRequiredService<OllamaBootstrap>().ChatOptions);
        services.AddSingleton(sp => sp.GetRequiredService<OllamaBootstrap>().OllamaSettings);

        services.AddHttpClient(nameof(OllamaModelCatalog), (sp, client) =>
        {
            var settings = sp.GetRequiredService<OllamaSettings>();
            client.BaseAddress = new Uri(settings.Endpoint);
        });
        services.AddSingleton<IOllamaModelCatalog, OllamaModelCatalog>();

        return services.AddOllamaChatServices();
    }

    public static IServiceCollection AddOllamaChatServices(
        this IServiceCollection services,
        Kernel kernel,
        ChatOptions chatOptions,
        OllamaSettings ollamaSettings)
    {
        services.AddSingleton(kernel);
        services.AddSingleton(chatOptions);
        services.AddSingleton(ollamaSettings);

        return services.AddOllamaChatServices();
    }

    public static IServiceCollection AddOllamaChatServices(this IServiceCollection services)
    {
        services.AddSingleton<IChatSessionStore, InMemoryChatSessionStore>();
        services.AddSingleton<ISystemPromptBuilder, SystemPromptBuilder>();
        services.AddSingleton<ISessionAttachmentService, SessionAttachmentService>();

        services.AddScoped<IUserChatContext>(sp =>
        {
            var options = sp.GetRequiredService<ChatOptions>();
            return new UserChatContext(options.DefaultUserId);
        });
        services.AddScoped<IChatSessionManager, ChatSessionManager>();
        services.AddScoped<ChatService>();

        services.AddSingleton<IChatMiddleware, LoggingChatMiddleware>();
        services.AddSingleton<IChatMiddleware, CurrentDateSystemPromptMiddleware>();

        return services;
    }
}
