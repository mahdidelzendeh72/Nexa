using System.Text;
using System.Text.Json;

namespace Maf.Agent.Api.Chat.Infrastructure.Middleware;

public readonly record struct AgUiTraceSignal(
    string Type,
    string? ThreadId,
    string? RunId,
    string? RefId,
    string? Name,
    int Size,
    string? Outcome,
    string? Model,
    long? InputTokens,
    long? OutputTokens,
    long? ReasoningTokens,
    long? TotalTokens);

public static class AgUiTraceSignals
{
    public static bool TryRead(ReadOnlySpan<byte> frame, out AgUiTraceSignal signal)
    {
        signal = default;
        if (!TryPayload(frame, out ReadOnlySpan<byte> json))
        {
            return false;
        }

        ReadOnlySpan<byte> typeHead = json.Length > 180 ? json[..180] : json;
        if (!TryField(typeHead, "type"u8, out ReadOnlySpan<byte> typeBytes))
        {
            return false;
        }

        string type = Encoding.UTF8.GetString(typeBytes);
        if (type is "RUN_STARTED" or "RUN_FINISHED")
        {
            signal = ReadRun(type, json);
            return true;
        }

        if (type.StartsWith("REASONING_", StringComparison.Ordinal))
        {
            ReadOnlySpan<byte> head = json.Length > 240 ? json[..240] : json;
            TryField(head, "entityId"u8, out ReadOnlySpan<byte> entityId);
            signal = new AgUiTraceSignal(type, null, null, Text(entityId), null, json.Length, null, null, null, null, null, null);
            return true;
        }

        int rawEvent = json.IndexOf("\"rawEvent\""u8);
        ReadOnlySpan<byte> fields = rawEvent >= 0 ? json[..rawEvent] : json;
        TryField(fields, "toolCallId"u8, out ReadOnlySpan<byte> toolCallId);
        TryField(fields, "messageId"u8, out ReadOnlySpan<byte> messageId);
        TryField(fields, "toolCallName"u8, out ReadOnlySpan<byte> toolCallName);
        TryField(fields, "delta"u8, out ReadOnlySpan<byte> delta);
        signal = new AgUiTraceSignal(
            type,
            null,
            null,
            toolCallId.Length > 0 ? Text(toolCallId) : Text(messageId),
            Text(toolCallName),
            delta.Length,
            null,
            null,
            null,
            null,
            null,
            null);
        return true;
    }

    private static AgUiTraceSignal ReadRun(string type, ReadOnlySpan<byte> json)
    {
        string? threadId = null;
        string? runId = null;
        string? outcome = null;
        string? model = null;
        long? input = null;
        long? output = null;
        long? reasoning = null;
        long? total = null;
        try
        {
            Utf8JsonReader reader = new(json);
            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                string? property = reader.GetString();
                if (!reader.Read())
                {
                    break;
                }

                switch (property)
                {
                    case "threadId" when reader.TokenType == JsonTokenType.String:
                        threadId = reader.GetString();
                        break;
                    case "runId" when reader.TokenType == JsonTokenType.String:
                        runId = reader.GetString();
                        break;
                    case "outcome" when reader.TokenType == JsonTokenType.StartObject:
                        outcome = ReadOutcome(ref reader);
                        break;
                    case "usage" when reader.TokenType == JsonTokenType.StartArray:
                        ReadUsage(ref reader, ref model, ref input, ref output, ref reasoning, ref total);
                        break;
                }
            }
        }
        catch (JsonException)
        {
        }

        return new AgUiTraceSignal(type, threadId, runId, null, null, 0, outcome, model, input, output, reasoning, total);
    }

    private static string? ReadOutcome(ref Utf8JsonReader reader)
    {
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName &&
                reader.GetString() == "type" &&
                reader.Read() &&
                reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }
        }

        return null;
    }

    private static void ReadUsage(
        ref Utf8JsonReader reader,
        ref string? model,
        ref long? input,
        ref long? output,
        ref long? reasoning,
        ref long? total)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            string? property = reader.GetString();
            if (!reader.Read())
            {
                return;
            }

            switch (property)
            {
                case "model" when reader.TokenType == JsonTokenType.String:
                    model = reader.GetString();
                    break;
                case "inputTokens" when reader.TryGetInt64(out long inputTokens):
                    input = inputTokens;
                    break;
                case "outputTokens" when reader.TryGetInt64(out long outputTokens):
                    output = outputTokens;
                    break;
                case "reasoningTokens" when reader.TryGetInt64(out long reasoningTokens):
                    reasoning = reasoningTokens;
                    break;
                case "totalTokens" when reader.TryGetInt64(out long totalTokens):
                    total = totalTokens;
                    break;
            }
        }
    }

    private static bool TryPayload(ReadOnlySpan<byte> frame, out ReadOnlySpan<byte> json)
    {
        int data = frame.IndexOf("data:"u8);
        if (data < 0)
        {
            json = default;
            return false;
        }

        int start = data + "data:"u8.Length;
        if (start < frame.Length && frame[start] == (byte)' ')
        {
            start++;
        }

        int lineEnd = frame[start..].IndexOf((byte)'\n');
        json = lineEnd < 0 ? frame[start..] : frame.Slice(start, lineEnd);
        if (json.Length > 0 && json[^1] == (byte)'\r')
        {
            json = json[..^1];
        }

        return json.Length > 0 && json[0] == (byte)'{';
    }

    private static bool TryField(ReadOnlySpan<byte> json, ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value)
    {
        Span<byte> pattern = stackalloc byte[key.Length + 2];
        pattern[0] = (byte)'"';
        key.CopyTo(pattern[1..]);
        pattern[^1] = (byte)'"';
        int at = json.IndexOf(pattern);
        if (at < 0)
        {
            value = default;
            return false;
        }

        int index = at + pattern.Length;
        while (index < json.Length && json[index] is (byte)' ' or (byte)'\t')
        {
            index++;
        }

        if (index >= json.Length || json[index] != (byte)':')
        {
            value = default;
            return false;
        }

        index++;
        while (index < json.Length && json[index] == (byte)' ')
        {
            index++;
        }

        if (index >= json.Length || json[index] != (byte)'"')
        {
            value = default;
            return false;
        }

        index++;
        int start = index;
        while (index < json.Length && json[index] != (byte)'"')
        {
            if (json[index] == (byte)'\\' && index + 1 < json.Length)
            {
                index += 2;
                continue;
            }

            index++;
        }

        value = json[start..index];
        return true;
    }

    private static string? Text(ReadOnlySpan<byte> value) =>
        value.Length == 0 ? null : Encoding.UTF8.GetString(value);
}
