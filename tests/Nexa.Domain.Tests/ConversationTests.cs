using FluentAssertions;
using Nexa.Domain.Conversations;

namespace Nexa.Domain.Tests;

public sealed class ConversationTests
{
    [Fact]
    public void First_user_message_becomes_title()
    {
        var conversation = Conversation.Create(Guid.CreateVersion7(), Guid.CreateVersion7());

        conversation.AddMessage(MessageRole.User, "How do I cancel a booking?");

        conversation.Title.Should().Be("How do I cancel a booking?");
    }

    [Fact]
    public void EnsureOwnedBy_rejects_other_users()
    {
        var owner = Guid.CreateVersion7();
        var conversation = Conversation.Create(owner, Guid.CreateVersion7());

        var act = () => conversation.EnsureOwnedBy(Guid.CreateVersion7());

        act.Should().Throw<UnauthorizedAccessException>();
        conversation.Invoking(c => c.EnsureOwnedBy(owner)).Should().NotThrow();
    }
}
