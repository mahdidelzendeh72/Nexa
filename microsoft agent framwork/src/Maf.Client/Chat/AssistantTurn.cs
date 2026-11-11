using System.Text.Json;
using AGUI.Abstractions;
using Microsoft.Extensions.AI;

namespace Maf.Client.Chat;

public sealed class AssistantTurn
{
    public bool Waiting { get; private set; } = true;

    public List<TurnPart> Parts { get; } = [];

    public string AnswerText => string.Concat(Parts.OfType<AnswerPart>().Select(part => part.Text));

    public static AssistantTurn FromStored(
        string text,
        long? inputTokens,
        long? outputTokens,
        long? reasoningTokens,
        long? cachedInputTokens,
        long? totalTokens,
        long? cacheWriteInputTokens)
    {
        AssistantTurn turn = new() { Waiting = false };
        if (!string.IsNullOrWhiteSpace(text))
        {
            turn.Parts.Add(new AnswerPart { Text = text });
        }

        turn.SetUsage(inputTokens, outputTokens, reasoningTokens, cachedInputTokens, totalTokens, cacheWriteInputTokens);
        return turn;
    }

    public void Apply(ChatResponseUpdate update)
    {
        if (update.Contents.Count > 0)
        {
            foreach (AIContent content in update.Contents)
            {
                ApplyContent(content);
            }

            return;
        }

        ApplyEvent(update.RawRepresentation);
    }

    public void Finish() => Waiting = false;

    public void ShowOutcome(string text)
    {
        Parts.Clear();
        Waiting = false;
        Parts.Add(new AnswerPart { Text = text });
    }

    public void SetUsage(
        long? inputTokens,
        long? outputTokens,
        long? reasoningTokens,
        long? cachedInputTokens,
        long? totalTokens,
        long? cacheWriteInputTokens)
    {
        if (inputTokens is null &&
            outputTokens is null &&
            reasoningTokens is null &&
            cachedInputTokens is null &&
            totalTokens is null &&
            cacheWriteInputTokens is null)
        {
            return;
        }

        UsagePart usage = Parts.OfType<UsagePart>().FirstOrDefault() ?? Add<UsagePart>();
        usage.InputTokens = inputTokens;
        usage.OutputTokens = outputTokens;
        usage.ReasoningTokens = reasoningTokens;
        usage.CachedInputTokens = cachedInputTokens;
        usage.TotalTokens = totalTokens;
        usage.CacheWriteInputTokens = cacheWriteInputTokens;
    }

    private void ApplyContent(AIContent content)
    {
        switch (content)
        {
            case TextContent text when !string.IsNullOrEmpty(text.Text):
                AppendAnswer(text.Text);
                break;
            case TextReasoningContent reasoning:
                if (!string.IsNullOrEmpty(reasoning.Text))
                    AppendReasoning(reasoning.Text);
                break;
            case FunctionCallContent call:
                Tool(call.CallId, call.Name).Arguments = FormatArguments(call.Arguments);
                Waiting = false;
                break;
            case FunctionResultContent result:
                Tool(result.CallId, "ابزار").Result = FormatResult(result.Result);
                Waiting = false;
                break;
            case UsageContent usage:
                AddReportedUsage(usage.Details);
                break;
            case ErrorContent error:
                Notice(string.IsNullOrWhiteSpace(error.Message) ? "خطای اجرا" : error.Message);
                break;
            case ToolApprovalRequestContent approval:
                NoteApproval(approval);
                break;
            case InterruptRequestContent interrupt:
                Notice(interrupt.Message ?? interrupt.Reason ?? "ورودی کاربر لازم است");
                break;
            case DataContent data:
                AddData(data);
                break;
            case UriContent uri:
                AddUri(uri);
                break;
            case HostedFileContent file:
                Notice(string.IsNullOrWhiteSpace(file.Name) ? $"فایل میزبان {file.FileId}" : file.Name);
                break;
            default:
                if (content.GetType().Name != nameof(TextReasoningContent))
                    Notice(content.GetType().Name);
                break;
        }
    }

    private void ApplyEvent(object? raw)
    {
        switch (raw)
        {
            case null:
            case RunStartedEvent:
            case RunFinishedEvent:
            case ReasoningEncryptedValueEvent:
                return;
            case ReasoningStartEvent:
            case ReasoningMessageStartEvent:
                OpenReasoning();
                break;
            case ReasoningMessageChunkEvent chunk when !string.IsNullOrEmpty(chunk.Delta):
                AppendReasoning(chunk.Delta);
                break;
            case ReasoningMessageEndEvent:
            case ReasoningEndEvent:
                CloseReasoning();
                break;
            case StepStartedEvent step:
                Notice($"مرحله شروع شد: {step.StepName}");
                break;
            case StepFinishedEvent step:
                Notice($"مرحله تمام شد: {step.StepName}");
                break;
            case CustomEvent custom:
                Notice(string.IsNullOrWhiteSpace(custom.Name) ? custom.Type : custom.Name);
                break;
            case ActivitySnapshotEvent activity:
                Notice(string.IsNullOrWhiteSpace(activity.ActivityType) ? activity.Type : activity.ActivityType);
                break;
            case ActivityDeltaEvent activity:
                Notice(string.IsNullOrWhiteSpace(activity.ActivityType) ? activity.Type : activity.ActivityType);
                break;
            case SubagentStartedEvent subagent:
                Notice(string.IsNullOrWhiteSpace(subagent.Name) ? "زیربخش شروع شد" : $"زیربخش شروع شد: {subagent.Name}");
                break;
            case SubagentFinishedEvent:
                Notice("زیربخش تمام شد");
                break;
            case SubagentErrorEvent:
                Notice("زیربخش با خطا تمام شد");
                break;
            case StateSnapshotEvent:
            case StateDeltaEvent:
                Notice("وضعیت گفتگو به‌روز شد");
                break;
            case BaseEvent evt:
                Notice(evt.Type);
                break;
            default:
                if (raw.GetType().Name != nameof(TextReasoningContent))
                    Notice(raw.GetType().Name);
                break;
        }
    }

    private void AppendAnswer(string text)
    {
        if (IsTypeLabel(text))
            return;

        Waiting = false;
        if (Parts.LastOrDefault() is AnswerPart answer)
        {
            answer.Text += text;
            return;
        }

        Parts.Add(new AnswerPart { Text = text });
    }

    private void OpenReasoning()
    {
        Waiting = false;
        if (Parts.LastOrDefault() is ReasoningPart { Closed: false })
        {
            return;
        }

        Parts.Add(new ReasoningPart());
    }

    private void AppendReasoning(string text)
    {
        if (IsTypeLabel(text))
            return;

        Waiting = false;
        if (Parts.LastOrDefault() is not ReasoningPart { Closed: false } reasoning)
        {
            reasoning = new ReasoningPart();
            Parts.Add(reasoning);
        }

        reasoning.Text += text;
    }

    private void CloseReasoning()
    {
        if (Parts.LastOrDefault() is ReasoningPart reasoning)
        {
            reasoning.Closed = true;
        }
    }

    private ToolPart Tool(string callId, string name)
    {
        ToolPart? tool = Parts.OfType<ToolPart>().LastOrDefault(part => part.CallId == callId);
        if (tool is not null)
        {
            if (!string.IsNullOrWhiteSpace(name) && name != "ابزار")
            {
                tool.Name = name;
            }

            return tool;
        }

        tool = new ToolPart { CallId = callId, Name = string.IsNullOrWhiteSpace(name) ? "ابزار" : name };
        Parts.Add(tool);
        return tool;
    }

    private void NoteApproval(ToolApprovalRequestContent approval)
    {
        Waiting = false;
        if (approval.ToolCall is FunctionCallContent call)
        {
            ToolPart tool = Tool(call.CallId, call.Name);
            tool.Arguments = FormatArguments(call.Arguments);
            tool.NeedsApproval = true;
            return;
        }

        Notice($"تأیید ابزار لازم است: {approval.ToolCall.GetType().Name}");
    }

    private void AddReportedUsage(UsageDetails details)
    {
        UsagePart usage = Parts.OfType<UsagePart>().FirstOrDefault() ?? Add<UsagePart>();
        usage.InputTokens = Sum(usage.InputTokens, details.InputTokenCount);
        usage.OutputTokens = Sum(usage.OutputTokens, details.OutputTokenCount);
        usage.ReasoningTokens = Sum(usage.ReasoningTokens, details.ReasoningTokenCount);
        usage.CachedInputTokens = Sum(usage.CachedInputTokens, details.CachedInputTokenCount);
        usage.TotalTokens = Sum(usage.TotalTokens, details.TotalTokenCount);
        usage.CacheWriteInputTokens = Sum(usage.CacheWriteInputTokens, CacheWrite(details));
    }

    private void AddData(DataContent data)
    {
        Waiting = false;
        if (data.HasTopLevelMediaType("image"))
        {
            Parts.Add(new MediaPart { Source = data.Uri, Alt = data.Name ?? "image" });
            return;
        }

        Notice(string.IsNullOrWhiteSpace(data.Name) ? data.MediaType : data.Name);
    }

    private void AddUri(UriContent uri)
    {
        Waiting = false;
        if (uri.HasTopLevelMediaType("image"))
        {
            Parts.Add(new MediaPart { Source = uri.Uri.ToString(), Alt = "image" });
            return;
        }

        Notice(uri.Uri.ToString());
    }

    private void Notice(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || IsTypeLabel(text))
        {
            return;
        }

        Waiting = false;
        Parts.Add(new NoticePart { Text = text });
    }

    private T Add<T>() where T : TurnPart, new()
    {
        T part = new();
        Parts.Add(part);
        return part;
    }

    private static bool IsTypeLabel(string? text) =>
        string.Equals(text?.Trim(), nameof(TextReasoningContent), StringComparison.Ordinal);

    private static string FormatArguments(IDictionary<string, object?>? arguments) =>
        arguments is null || arguments.Count == 0
            ? ""
            : JsonSerializer.Serialize(arguments, new JsonSerializerOptions { WriteIndented = true });

    private static string FormatResult(object? result) =>
        result switch
        {
            null => "",
            string text => text,
            _ => result.ToString() ?? "",
        };

    private static long? CacheWrite(UsageDetails details) =>
        details.AdditionalCounts is { } counts &&
        counts.TryGetValue("CacheWriteInputTokens", out long count)
            ? count
            : null;

    private static long? Sum(long? accumulated, long? reported) =>
        reported is null or < 0 ? accumulated : (accumulated ?? 0) + reported.Value;
}

public abstract class TurnPart;

public sealed class AnswerPart : TurnPart
{
    public string Text { get; set; } = "";
}

public sealed class ReasoningPart : TurnPart
{
    public string Text { get; set; } = "";

    public bool Closed { get; set; }
}

public sealed class ToolPart : TurnPart
{
    public string CallId { get; set; } = "";

    public string Name { get; set; } = "";

    public string Arguments { get; set; } = "";

    public string? Result { get; set; }

    public bool NeedsApproval { get; set; }
}

public sealed class UsagePart : TurnPart
{
    public long? InputTokens { get; set; }

    public long? OutputTokens { get; set; }

    public long? ReasoningTokens { get; set; }

    public long? CachedInputTokens { get; set; }

    public long? TotalTokens { get; set; }

    public long? CacheWriteInputTokens { get; set; }

    public string Text
    {
        get
        {
            List<string> parts = [];
            if (InputTokens is long input)
            {
                parts.Add($"ورودی {input}");
            }

            if (OutputTokens is long output)
            {
                parts.Add($"خروجی {output}");
            }

            if (ReasoningTokens is long reasoning)
            {
                parts.Add($"استدلال {reasoning}");
            }

            if (CachedInputTokens is long cached)
            {
                parts.Add($"کش {cached}");
            }

            if (CacheWriteInputTokens is long written)
            {
                parts.Add($"نوشتن کش {written}");
            }

            if (TotalTokens is long total)
            {
                parts.Add($"کل {total}");
            }

            return string.Join(" · ", parts);
        }
    }
}

public sealed class NoticePart : TurnPart
{
    public string Text { get; set; } = "";
}

public sealed class MediaPart : TurnPart
{
    public string Source { get; set; } = "";

    public string Alt { get; set; } = "";
}
