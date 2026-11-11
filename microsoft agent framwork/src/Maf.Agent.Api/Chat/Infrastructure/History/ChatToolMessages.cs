using System.Text.Json;
using Maf.Agent.Api.Chat.Domain;
using Microsoft.Extensions.AI;

namespace Maf.Agent.Api.Chat.Infrastructure.History;

public static class ChatToolMessages
{
    private const int ResultLimit = 20_000;

    public static IReadOnlyList<string> Capture(IEnumerable<ChatMessage>? messages)
    {
        if (messages is null)
        {
            return [];
        }

        List<string> payloads = [];
        foreach (ChatMessage message in messages)
        {
            List<AIContent> tools = [];
            foreach (AIContent content in message.Contents)
            {
                if (Copy(content) is AIContent tool)
                {
                    tools.Add(tool);
                }
            }

            if (tools.Count == 0)
            {
                continue;
            }

            string? payload = Fit(new ChatMessage(message.Role, tools));
            if (payload is not null)
            {
                payloads.Add(payload);
            }
        }

        return payloads;
    }

    public static ChatMessage? Read(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize(payload, AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(ChatMessage))) as ChatMessage;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static AIContent? Copy(AIContent content) => content switch
    {
        FunctionCallContent call => new FunctionCallContent(call.CallId, call.Name, ArgumentsOf(call.Arguments)),
        FunctionResultContent result => new FunctionResultContent(result.CallId, TextOf(result.Result)),
        _ => null,
    };

    private static IDictionary<string, object?>? ArgumentsOf(IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return null;
        }

        Dictionary<string, object?> copy = [];
        foreach ((string key, object? value) in arguments)
        {
            copy[key] = value switch
            {
                null or string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal or JsonElement => value,
                _ => TextOf(value),
            };
        }

        return copy;
    }

    private static string? Fit(ChatMessage message)
    {
        try
        {
            string json = Serialize(message);
            if (json.Length <= ChatToolMessage.MaxPayloadLength)
            {
                return json;
            }

            message = Shrink(message);
            json = Serialize(message);
            return json.Length <= ChatToolMessage.MaxPayloadLength ? json : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static ChatMessage Shrink(ChatMessage message)
    {
        List<AIContent> contents = [];
        foreach (AIContent content in message.Contents)
        {
            switch (content)
            {
                case FunctionResultContent result:
                    string text = TextOf(result.Result);
                    if (text.Length > ResultLimit)
                    {
                        text = text[..ResultLimit];
                    }

                    contents.Add(new FunctionResultContent(result.CallId, text));
                    break;
                case FunctionCallContent call:
                    contents.Add(new FunctionCallContent(call.CallId, call.Name));
                    break;
            }
        }

        return new ChatMessage(message.Role, contents);
    }

    private static string TextOf(object? result)
    {
        switch (result)
        {
            case null:
                return "";
            case string text:
                return text;
            case JsonElement element:
                return element.GetRawText();
            default:
                try
                {
                    return JsonSerializer.Serialize(result, AIJsonUtilities.DefaultOptions.GetTypeInfo(result.GetType()));
                }
                catch (NotSupportedException)
                {
                    return result.ToString() ?? "";
                }
        }
    }

    private static string Serialize(ChatMessage message) =>
        JsonSerializer.Serialize(message, AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(ChatMessage)));
}
