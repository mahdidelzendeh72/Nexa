using Maf.Agent.Api.Chat.Application;

namespace Maf.Agent.Api.Tests;

public sealed class ChatToolSelectionTests
{
    [Fact]
    public void Parse_keeps_known_shaped_keys_and_skips_the_rest()
    {
        Guid serverId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        string encoded = $"{serverId:D}|{Uri.EscapeDataString("read|file")}";

        IReadOnlyList<ChatToolKey> keys = ChatToolSelection.Parse(
        [
            encoded,
            encoded,
            "not-a-guid|tool",
            $"{serverId:D}|",
            "   ",
        ]);

        ChatToolKey key = Assert.Single(keys);
        Assert.Equal(serverId, key.ServerId);
        Assert.Equal("read|file", key.Name);
    }

    [Fact]
    public void Parse_splits_a_comma_joined_header()
    {
        Guid first = Guid.Parse("33333333-3333-3333-3333-333333333333");
        Guid second = Guid.Parse("44444444-4444-4444-4444-444444444444");

        IReadOnlyList<ChatToolKey> keys = ChatToolSelection.Parse(
            [$"{first:D}|alpha,{second:D}|beta"]);

        Assert.Collection(
            keys,
            key => Assert.Equal("alpha", key.Name),
            key => Assert.Equal("beta", key.Name));
    }

    [Fact]
    public void Parse_stops_at_the_maximum_count()
    {
        Guid serverId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        string[] values = Enumerable.Range(0, ChatToolSelection.MaximumCount + 5)
            .Select(index => $"{serverId:D}|tool-{index}")
            .ToArray();

        IReadOnlyList<ChatToolKey> keys = ChatToolSelection.Parse(values);

        Assert.Equal(ChatToolSelection.MaximumCount, keys.Count);
    }
}
