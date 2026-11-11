using Maf.Agent.Api.Chat.Application;
using Maf.Agent.Api.Chat.Domain;
using Microsoft.Extensions.AI;

namespace Maf.Agent.Api.Tests;

public class AssistantReplyFilesTests
{
    [Fact]
    public void Readable_bytes_and_web_links_are_kept()
    {
        ChatMessage message = new(
            ChatRole.Assistant,
            [
                new DataContent(new byte[] { 1, 2, 3 }, "image/png") { Name = "shot.png" },
                new UriContent(new Uri("https://example.com/a"), "text/plain"),
                new HostedFileContent("file_123"),
                new UriContent(new Uri("javascript:alert(1)"), "text/plain"),
                new DataContent(new byte[] { 9 }, "text/plain"),
            ]);

        IReadOnlyList<ChatFile> files = AssistantReplyFiles.Read(
            [new ChatMessage(ChatRole.User, [new DataContent(new byte[] { 4 }, "text/plain")]), message]);

        Assert.Equal(3, files.Count);
        Assert.Equal("shot.png", files[0].FileName);
        Assert.Equal("image/png", files[0].MediaType);
        Assert.Equal("text/uri-list", files[1].MediaType);
        Assert.Equal("https://example.com/a", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(files[1].ContentBase64)));
        Assert.Equal("file.plain", files[2].FileName);
    }

    [Fact]
    public void Only_the_first_four_reply_files_are_kept()
    {
        List<AIContent> contents = [];
        for (int index = 0; index < 5; index++)
        {
            contents.Add(new DataContent(new byte[] { 1 }, "text/plain") { Name = $"{index}.txt" });
        }

        IReadOnlyList<ChatFile> files = AssistantReplyFiles.Read([new ChatMessage(ChatRole.Assistant, contents)]);

        Assert.Equal(4, files.Count);
        Assert.Equal("0.txt", files[0].FileName);
        Assert.Equal("3.txt", files[3].FileName);
    }
}
