using Maf.Agent.Api.Chat.Infrastructure.History;
using Microsoft.Extensions.AI;

namespace Maf.Agent.Api.Tests;

public sealed class ChatToolMessageTests
{
    [Fact]
    public void Capture_keeps_the_call_and_the_result_for_the_model()
    {
        ChatMessage call = new(
            ChatRole.Assistant,
            [new FunctionCallContent("call-1", "lookup", new Dictionary<string, object?> { ["q"] = "آب" })]);
        ChatMessage result = new(ChatRole.Tool, [new FunctionResultContent("call-1", "پیدا شد")]);
        ChatMessage answer = new(ChatRole.Assistant, "تمام");

        IReadOnlyList<string> payloads = ChatToolMessages.Capture([call, result, answer]);

        Assert.Equal(2, payloads.Count);
        ChatMessage restoredCall = ChatToolMessages.Read(payloads[0])!;
        FunctionCallContent restored = Assert.Single(restoredCall.Contents.OfType<FunctionCallContent>());
        Assert.Equal("lookup", restored.Name);
        Assert.Equal("call-1", restored.CallId);
        Assert.Equal("آب", restored.Arguments!["q"]?.ToString());
        ChatMessage restoredResult = ChatToolMessages.Read(payloads[1])!;
        FunctionResultContent restoredOutput = Assert.Single(restoredResult.Contents.OfType<FunctionResultContent>());
        Assert.Equal("call-1", restoredOutput.CallId);
        Assert.Equal("پیدا شد", restoredOutput.Result?.ToString());
    }
}
