using Maf.Agent.Api.Chat.Domain;
using Microsoft.EntityFrameworkCore;

namespace Maf.Agent.Api.Chat.Infrastructure.History;

public static class ChatHistoryConfiguration
{
    public static ModelBuilder ConfigureChatHistory(this ModelBuilder builder)
    {
        builder.Entity<ChatConversation>(entity =>
        {
            entity.ToTable("ChatConversations");
            entity.HasKey(conversation => conversation.ThreadId);
            entity.Property(conversation => conversation.ThreadId).HasMaxLength(ChatConversation.MaxThreadIdLength);
            entity.Property(conversation => conversation.OwnerUserId).HasMaxLength(450);
            entity.Property(conversation => conversation.Surface).HasMaxLength(32);
            entity.Property(conversation => conversation.Title).HasMaxLength(ChatConversation.MaxTitleLength);
            entity.Property(conversation => conversation.ModelName).HasMaxLength(200);
            entity.Property(conversation => conversation.ServiceConversationId)
                .HasMaxLength(ChatConversation.MaxServiceConversationIdLength);
            entity.HasIndex(conversation => new { conversation.OwnerUserId, conversation.Surface, conversation.UpdatedAt });
            entity.Navigation(conversation => conversation.Entries).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(conversation => conversation.Entries)
                .WithOne()
                .HasForeignKey(entry => entry.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChatEntry>(entity =>
        {
            entity.ToTable("ChatEntries");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Role).HasConversion<string>().HasMaxLength(16);
            entity.Property(entry => entry.Reaction)
                .HasConversion<string>()
                .HasMaxLength(16)
                .HasDefaultValue(ChatReaction.None);
            entity.Property(entry => entry.Text);
            entity.HasIndex(entry => new { entry.ThreadId, entry.Sequence }).IsUnique();
            entity.Navigation(entry => entry.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(entry => entry.Attachments)
                .WithOne()
                .HasForeignKey(attachment => attachment.EntryId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(entry => entry.Usage)
                .WithOne()
                .HasForeignKey<ChatTurnUsage>(usage => usage.EntryId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(entry => entry.ToolMessages).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(entry => entry.ToolMessages)
                .WithOne()
                .HasForeignKey(message => message.EntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChatToolMessage>(entity =>
        {
            entity.ToTable("ChatToolMessages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Payload);
            entity.HasIndex(message => new { message.EntryId, message.Sequence }).IsUnique();
        });

        builder.Entity<ChatTurnUsage>(entity =>
        {
            entity.ToTable("ChatTurnUsages");
            entity.HasKey(usage => usage.EntryId);
            entity.Property(usage => usage.EntryId).ValueGeneratedNever();
        });

        builder.Entity<ChatAttachment>(entity =>
        {
            entity.ToTable("ChatAttachments");
            entity.HasKey(attachment => attachment.Id);
            entity.Property(attachment => attachment.FileName).HasMaxLength(ChatFile.MaxFileNameLength);
            entity.Property(attachment => attachment.MediaType).HasMaxLength(ChatFile.MaxMediaTypeLength);
            entity.HasIndex(attachment => new { attachment.EntryId, attachment.Sequence }).IsUnique();
        });

        return builder;
    }
}
