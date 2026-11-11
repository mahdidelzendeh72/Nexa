using System.ClientModel;
using Maf.Agent.Api.Chat.Application;
using Maf.Agent.Api.Models.Domain;
using Maf.Agent.Api.Models.Infrastructure;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace Maf.Agent.Api.Chat.Infrastructure.Clients;

public static class ProviderChatClient
{
    public static IChatClient Create(ChatConnection connection)
    {
        return connection.Kind switch
        {
            ModelProviderKind.Ollama => CreateOllama(connection),
            ModelProviderKind.OpenAI or ModelProviderKind.OpenAICompatible => CreateOpenAi(connection),
            _ => throw new InvalidOperationException($"ارائه‌دهنده «{connection.Kind}» برای گفتگو پشتیبانی نمی‌شود."),
        };
    }

    private static OllamaApiClient CreateOllama(ChatConnection connection)
    {
        OllamaApiClient client = new(OllamaModelCatalog.Root(connection.BaseAddress), connection.ModelName);
        if (!string.IsNullOrEmpty(connection.ApiKey))
        {
            client.DefaultRequestHeaders["Authorization"] = "Bearer " + connection.ApiKey;
        }

        return client;
    }

    private static IChatClient CreateOpenAi(ChatConnection connection)
    {
        string apiKey = string.IsNullOrEmpty(connection.ApiKey) ? "local" : connection.ApiKey;
        OpenAIClient transport = new(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = OpenAiModelCatalog.ServiceRoot(connection.BaseAddress) });

#pragma warning disable OPENAI001 // Responses API در SDK اوپن‌ای هنوز experimental است.
        return connection.Protocol switch
        {
            ModelProtocol.ChatCompletions => transport.GetChatClient(connection.ModelName).AsIChatClient(),
            ModelProtocol.Responses => transport.GetResponsesClient().AsIChatClient(connection.ModelName),
            _ => throw new InvalidOperationException("پروتکل باید Responses API یا Chat Completions باشد."),
        };
#pragma warning restore OPENAI001
    }
}
