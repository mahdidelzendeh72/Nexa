using Maf.Agent.Api.Chat.Application;
using Maf.Agent.Api.Chat.Domain;
using Maf.Agent.Api.Chat.Endpoints;
using Maf.Agent.Api.Chat.Infrastructure.Clients;
using Maf.Agent.Api.Chat.Infrastructure.ContextProviders;
using Maf.Agent.Api.Chat.Infrastructure.History;
using Maf.Agent.Api.Chat.Infrastructure.Ledgers;
using Maf.Agent.Api.Chat.Infrastructure.Middleware;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;

namespace Maf.Agent.Api.Chat;

public static class ChatFeature
{
    public static IServiceCollection AddChatFeature(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddAGUIServer();
        services.AddSingleton<ChatModelCatalog>();
        services.AddSingleton<ChatToolCatalog>();
        services.AddSingleton<SelectedChatClient>();
        services.AddSingleton<StoredChatHistoryProvider>();
        services.AddSingleton<ChatToolContextProvider>();
        services.AddSingleton<ChatUsageLedger>();
        services.AddSingleton<ChatReplyFileLedger>();
        services.AddSingleton<ChatTurnRecorder>();
        services.AddSingleton<ToolCallMiddleware>();
        services.AddSingleton<ChatTraceLedger>();
        services.AddSingleton<IStartupFilter, AgUiEventLogStartupFilter>();
        services.AddScoped<IChatTraceStore, PostgresChatTraceStore>();
        services.AddScoped<ChatTraceReader>();
        services.AddHostedService<ChatTraceFlushWorker>();
        services.AddSingleton<ThreadStampSessionStore>();
        services.AddScoped<IChatHistoryStore, PostgresChatHistoryStore>();
#pragma warning disable MAAI001 // AgentSessionStore is experimental in Microsoft Agent Framework.
        foreach (ChatSurface surface in ChatSurface.All)
        {
            services.AddKeyedSingleton<AgentSessionStore>(
                surface.Name,
                (provider, _) => provider.GetRequiredService<ThreadStampSessionStore>());
        }
#pragma warning restore MAAI001

        return services;
    }

    public static WebApplication MapChatFeature(this WebApplication app)
    {
        app.MapGet("/api/chat/models", async (ChatModelCatalog catalog, CancellationToken cancellationToken) =>
            Results.Ok(await catalog.ListAsync(cancellationToken)))
            .RequireAuthorization();
        app.MapGet("/api/chat/tools", async (ChatToolCatalog catalog, CancellationToken cancellationToken) =>
            Results.Ok(await catalog.ListAsync(cancellationToken)))
            .RequireAuthorization();

        app.MapChatHistoryEndpoints();
        app.MapChatTraceEndpoints();

        SelectedChatClient selected = app.Services.GetRequiredService<SelectedChatClient>();
        StoredChatHistoryProvider history = app.Services.GetRequiredService<StoredChatHistoryProvider>();
        ChatToolContextProvider tools = app.Services.GetRequiredService<ChatToolContextProvider>();
        ChatTurnRecorder recorder = app.Services.GetRequiredService<ChatTurnRecorder>();
        ToolCallMiddleware toolCalls = app.Services.GetRequiredService<ToolCallMiddleware>();
        foreach (ChatSurface surface in ChatSurface.All)
        {
            AIAgent agent = selected.AsAIAgent(new ChatClientAgentOptions
            {
                Name = surface.Name,
                ChatOptions = new ChatOptions { Instructions = ChatSurface.Instructions },
                ChatHistoryProvider = history,
                AIContextProviders = [tools, recorder],
                ThrowOnChatHistoryProviderConflict = false,
                ClearOnChatHistoryProviderConflict = false,
                WarnOnChatHistoryProviderConflict = false,
            }).AsBuilder().Use(toolCalls.Invoke).Build();
            FunctionInvokingChatClient invocation = agent.GetService<FunctionInvokingChatClient>()
                ?? throw new InvalidOperationException("حلقهٔ ابزار عامل پیدا نشد.");
            invocation.MaximumIterationsPerRequest = 100;
            app.MapAGUIServer(surface.Path, agent).RequireAuthorization();
        }

        return app;
    }
}
