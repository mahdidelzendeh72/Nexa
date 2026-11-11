using System.Text;
using Maf.Agent.Api.Chat.Domain;
using Microsoft.Extensions.AI;

namespace Maf.Agent.Api.Chat.Application;

public static class AssistantReplyFiles
{
    public const string LinkMediaType = "text/uri-list";

    public static IReadOnlyList<ChatFile> Read(IEnumerable<ChatMessage>? messages)
    {
        if (messages is null)
        {
            return [];
        }

        List<ChatFile> files = [];
        foreach (ChatMessage message in messages)
        {
            if (message.Role != ChatRole.Assistant)
            {
                continue;
            }

            foreach (AIContent content in message.Contents)
            {
                if (files.Count >= ChatFile.MaxPerMessage)
                {
                    return files;
                }

                if (FileOf(content) is ChatFile file)
                {
                    files.Add(file);
                }
            }
        }

        return files;
    }

    public static bool HasPendingToolCall(IEnumerable<ChatMessage>? messages) =>
        messages?.Any(message => message.Contents.OfType<FunctionCallContent>().Any()) == true;

    private static ChatFile? FileOf(AIContent content)
    {
        try
        {
            return content switch
            {
                DataContent data when data.Data.Length is > 0 and <= ChatFile.MaxBytes =>
                    new ChatFile(NameOf(data), data.MediaType, Convert.ToBase64String(data.Data.Span)),
                UriContent uri when WebAddress(uri.Uri) is string address =>
                    new ChatFile("link", LinkMediaType, Convert.ToBase64String(Encoding.UTF8.GetBytes(address))),
                _ => null,
            };
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string NameOf(DataContent data)
    {
        if (!string.IsNullOrWhiteSpace(data.Name))
        {
            return data.Name;
        }

        string subtype = data.MediaType.Split('/') is [_, string second, ..] ? second.Split('+')[0] : "bin";
        return string.IsNullOrWhiteSpace(subtype) ? "file" : "file." + subtype;
    }

    private static string? WebAddress(Uri uri) =>
        uri.IsAbsoluteUri && uri.Scheme is "http" or "https" ? uri.AbsoluteUri : null;
}
