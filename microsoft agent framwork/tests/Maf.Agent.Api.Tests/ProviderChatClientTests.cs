using Maf.Agent.Api.Chat.Application;
using Maf.Agent.Api.Chat.Infrastructure.Clients;
using Maf.Agent.Api.Models.Domain;
using Maf.Agent.Api.Models.Infrastructure;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace Maf.Agent.Api.Tests;

public sealed class ProviderChatClientTests
{
    [Fact]
    public void Ollama_uses_its_native_client_for_either_protocol()
    {
        using IDisposable chat = Create(ModelProviderKind.Ollama, ModelProtocol.ChatCompletions, "http://192.168.10.222:11434");
        using IDisposable responses = Create(ModelProviderKind.Ollama, ModelProtocol.Responses, "http://192.168.10.222:11434/v1");

        Assert.IsType<OllamaApiClient>(chat);
        Assert.IsType<OllamaApiClient>(responses);
    }

    [Fact]
    public void OpenAi_uses_the_openai_client_for_the_stored_protocol()
    {
        using IDisposable completions = Create(ModelProviderKind.OpenAI, ModelProtocol.ChatCompletions, "https://api.openai.com");
        using IDisposable responses = Create(ModelProviderKind.OpenAICompatible, ModelProtocol.Responses, "https://gateway.example");

        Assert.Equal("OpenAIChatClient", completions.GetType().Name);
        Assert.Equal("OpenAIResponsesChatClient", responses.GetType().Name);
    }

    [Fact]
    public void OpenAi_service_root_matches_the_models_endpoint()
    {
        Assert.Equal(
            "https://api.openai.com/v1",
            OpenAiModelCatalog.ServiceRoot(new Uri("https://api.openai.com")).AbsoluteUri);
        Assert.Equal(
            "https://gateway.example/v1",
            OpenAiModelCatalog.ServiceRoot(new Uri("https://gateway.example/v1")).AbsoluteUri);
    }

    private static IDisposable Create(ModelProviderKind kind, ModelProtocol protocol, string address)
    {
        IChatClient client = ProviderChatClient.Create(new ChatConnection(
            kind,
            protocol,
            "model",
            new Uri(address),
            kind == ModelProviderKind.Ollama ? null : "test-key"));
        return Assert.IsAssignableFrom<IDisposable>(client);
    }
}
