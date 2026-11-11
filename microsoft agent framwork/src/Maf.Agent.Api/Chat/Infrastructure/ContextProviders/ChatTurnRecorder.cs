using Maf.Agent.Api.Chat.Application;
using Maf.Agent.Api.Chat.Domain;
using Maf.Agent.Api.Chat.Infrastructure.Clients;
using Maf.Agent.Api.Chat.Infrastructure.History;
using Maf.Agent.Api.Chat.Infrastructure.Ledgers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Security.Claims;

namespace Maf.Agent.Api.Chat.Infrastructure.ContextProviders;

public sealed class ChatTurnRecorder(
    IHttpContextAccessor http,
    IServiceScopeFactory scopes,
    ChatUsageLedger usageLedger,
    ChatReplyFileLedger replyFiles,
    ILogger<ChatTurnRecorder> logger) : AIContextProvider
{
    protected override async ValueTask InvokedCoreAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        string? threadId = context.Session?.StateBag.TryGetValue(ThreadStampSessionStore.ThreadKey, out string? stamped) == true
            ? stamped
            : null;
        if (threadId is not null)
        {
            foreach (UsageDetails details in ChatRequestUsage.Drain(http.HttpContext))
            {
                usageLedger.Add(threadId, details);
            }
        }

        string? assistantText = context.InvokeException switch
        {
            null => AssistantText(context.ResponseMessages),
            OperationCanceledException => "عملیات توسط کاربر لغو شد",
            _ => "خطا نامشخص رخ داد",
        };
        IReadOnlyList<ChatFile> found = AssistantReplyFiles.Read(context.ResponseMessages);
        if (assistantText is null &&
            (found.Count == 0 || AssistantReplyFiles.HasPendingToolCall(context.ResponseMessages)))
        {
            if (threadId is not null)
            {
                replyFiles.Add(threadId, found);
            }

            return;
        }

        if (threadId is null ||
            http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) is not string ownerUserId ||
            context.Agent.Name is not string surface ||
            !ChatConversation.IsSurface(surface))
        {
            if (threadId is not null)
            {
                replyFiles.Add(threadId, found);
            }

            return;
        }

        ChatMessage? userMessage = LastUserMessage(context.RequestMessages);
        if (userMessage is null || (string.IsNullOrWhiteSpace(userMessage.Text) && !HasFile(userMessage)))
        {
            replyFiles.Add(threadId, found);
            return;
        }

        string userText = userMessage.Text ?? "";

        HttpContext httpContext = http.HttpContext;
        Guid? providerId = Guid.TryParse(httpContext.Request.Headers[SelectedChatClient.ProviderHeader], out Guid parsed)
            ? parsed
            : null;
        string? modelName = string.IsNullOrWhiteSpace(httpContext.Request.Headers[SelectedChatClient.ModelHeader])
            ? null
            : Uri.UnescapeDataString(httpContext.Request.Headers[SelectedChatClient.ModelHeader].ToString());
        string? serviceConversationId = context.Session is ChatClientAgentSession chatSession
            ? chatSession.ConversationId
            : null;

        ChatTurnUsage? usage = usageLedger.Take(threadId);
        IReadOnlyList<ChatFile> assistantFiles = replyFiles.Take(threadId, found);
        try
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            IChatHistoryStore store = scope.ServiceProvider.GetRequiredService<IChatHistoryStore>();
            await store.AppendTurnAsync(
                threadId,
                ownerUserId,
                surface,
                userText,
                assistantText ?? "",
                providerId,
                modelName,
                serviceConversationId,
                FilesOf(userMessage),
                assistantFiles,
                usage,
                ChatToolMessages.Capture(context.ResponseMessages),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            usageLedger.Add(threadId, usage);
            replyFiles.Add(threadId, assistantFiles);
            logger.LogWarning(exception, "گفتگو ذخیره نشد.");
        }
    }

    private static ChatMessage? LastUserMessage(IEnumerable<ChatMessage> messages)
    {
        ChatMessage? found = null;
        foreach (ChatMessage message in messages)
        {
            if (message.Role == ChatRole.User)
            {
                found = message;
            }
        }

        return found;
    }

    private static bool HasFile(ChatMessage message) =>
        message.Contents.OfType<DataContent>().Any(content => content.Data.Length > 0);

    private static List<ChatFile> FilesOf(ChatMessage message)
    {
        List<ChatFile> files = [];
        foreach (DataContent content in message.Contents.OfType<DataContent>())
        {
            if (content.Data.Length == 0)
            {
                continue;
            }

            files.Add(new ChatFile(content.Name, content.MediaType, Convert.ToBase64String(content.Data.Span)));
        }

        return files;
    }

    private static string? AssistantText(IEnumerable<ChatMessage>? messages)
    {
        if (messages is null)
        {
            return null;
        }

        string text = string.Join(
            "\n",
            messages.Where(message => message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Text))
                .Select(message => message.Text.Trim()));
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
