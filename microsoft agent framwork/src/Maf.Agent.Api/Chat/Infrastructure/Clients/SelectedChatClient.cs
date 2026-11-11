using Maf.Agent.Api.Chat.Application;
using Maf.Agent.Api.Chat.Infrastructure.Ledgers;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Maf.Agent.Api.Chat.Infrastructure.Clients;

public sealed class SelectedChatClient(ChatModelCatalog catalog, IHttpContextAccessor http) : IChatClient
{
    public const string ProviderHeader = "X-Chat-Provider";

    public const string ModelHeader = "X-Chat-Model";

    private readonly ConcurrentDictionary<string, IChatClient> _clients = new(StringComparer.Ordinal);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<AiChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        IChatClient client = await ClientFor(cancellationToken);
        ChatResponse response = await client.GetResponseAsync(messages, options, cancellationToken);
        if (response.Usage is not null)
        {
            ChatRequestUsage.Observe(http.HttpContext, response.Usage);
        }
        else
        {
            foreach (UsageContent content in response.Messages.SelectMany(message => message.Contents).OfType<UsageContent>())
            {
                ChatRequestUsage.Observe(http.HttpContext, content.Details);
            }
        }

        return response;
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<AiChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        StreamAsync(messages, options, cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
        foreach (IChatClient client in _clients.Values)
        {
            (client as IDisposable)?.Dispose();
        }

        _clients.Clear();
    }

    private async IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
        IEnumerable<AiChatMessage> messages,
        ChatOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IChatClient client = await ClientFor(cancellationToken);
        await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            foreach (UsageContent content in update.Contents.OfType<UsageContent>())
            {
                ChatRequestUsage.Observe(http.HttpContext, content.Details);
            }

            yield return update;
        }
    }

    private async Task<IChatClient> ClientFor(CancellationToken cancellationToken)
    {
        HttpRequest? request = http.HttpContext?.Request;
        if (request is null ||
            !Guid.TryParse(request.Headers[ProviderHeader], out Guid providerId) ||
            string.IsNullOrWhiteSpace(request.Headers[ModelHeader]))
        {
            throw new InvalidOperationException("یک مدل انتخاب کنید.");
        }

        string modelName = Uri.UnescapeDataString(request.Headers[ModelHeader].ToString());
        ChatConnection connection = await catalog.RequireAsync(providerId, modelName, cancellationToken);
        string key = string.Join(
            '\n',
            connection.Kind,
            connection.Protocol,
            connection.BaseAddress.AbsoluteUri,
            connection.ModelName,
            connection.ApiKey);
        return _clients.GetOrAdd(key, _ => ProviderChatClient.Create(connection));
    }
}
