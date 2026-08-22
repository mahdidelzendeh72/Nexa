using System.ClientModel;
using Microsoft.Extensions.AI;
using Nexa.Application.Abstractions;
using Nexa.Application.Common;
using Nexa.Domain.Models;
using OpenAI;

namespace Nexa.Infrastructure.Ai;

internal interface IChatClientFactory
{
    IChatClient Create(ModelProvider provider, ModelProfile profile);
}

internal sealed class OpenAiCompatibleChatClientFactory(IProviderConnectionResolver connections) : IChatClientFactory
{
    public IChatClient Create(ModelProvider provider, ModelProfile profile)
    {
        if (!provider.IsEnabled || !profile.IsEnabled)
        {
            throw new NexaException(ErrorCodes.ModelProviderNotConfigured, "The selected model is disabled.", 400);
        }

        if (!IsOpenAiCompatible(provider.Kind))
        {
            throw new NexaException(
                ErrorCodes.ModelProviderUnsupported,
                $"Provider kind '{provider.Kind}' is not available in Phase 2. Use an OpenAI-compatible endpoint.",
                501);
        }

        var connection = connections.Resolve(provider);
        var options = new OpenAIClientOptions { Endpoint = new Uri(connection.Endpoint, UriKind.Absolute) };
        var credential = new ApiKeyCredential(string.IsNullOrWhiteSpace(connection.ApiKey) ? "not-needed" : connection.ApiKey);
        return new OpenAIClient(credential, options).GetChatClient(profile.ModelId).AsIChatClient();
    }

    private static bool IsOpenAiCompatible(ModelProviderKind kind) =>
        kind is ModelProviderKind.OpenAICompatible
            or ModelProviderKind.OpenAI
            or ModelProviderKind.AzureOpenAI
            or ModelProviderKind.Ollama;
}
