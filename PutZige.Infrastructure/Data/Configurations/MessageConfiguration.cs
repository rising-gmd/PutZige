using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PutZige.Domain.Entities;
using PutZige.Application.Common.Constants;

namespace PutZige.Infrastructure.Data.Configurations
{
    public class MessageConfiguration : BaseEntityConfiguration<Message>
    {
        public override void Configure(EntityTypeBuilder<Message> builder)
        {
            base.Configure(builder);

            builder.ToTable("Messages");

            builder.Property(m => m.SenderId).IsRequired();
            builder.Property(m => m.ReceiverId).IsRequired();

            builder.Property(m => m.MessageText).IsRequired().HasMaxLength(AppConstants.Validation.MaxLongTextLength);

            builder.Property(m => m.SentAt).IsRequired();

            // Relationships
            builder.HasOne(m => m.Sender).WithMany(u => u.SentMessages).HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(m => m.Receiver).WithMany(u => u.ReceivedMessages).HasForeignKey(m => m.ReceiverId).OnDelete(DeleteBehavior.Restrict);

            // === PERFORMANCE INDEXES FOR REAL-TIME CHAT AT SCALE ===
            
            // 1. Inbox query: Get messages received by user, ordered by time (used for unread counts, inbox)
            builder.HasIndex(m => new { m.ReceiverId, m.SentAt })
                .HasDatabaseName("IX_Messages_ReceiverId_SentAt")
                .IsDescending(false, true);

            // 2. Conversation query: Get messages between two users with covering columns
            //    This index supports WHERE (SenderId=@A AND ReceiverId=@B) OR (SenderId=@B AND ReceiverId=@A)
            builder.HasIndex(m => new { m.SenderId, m.ReceiverId, m.SentAt })
                .HasDatabaseName("IX_Messages_Conversation")
                .IsDescending(false, false, true);

            // 3. Reverse conversation lookup: Allows SQL Server to use index for either direction
            builder.HasIndex(m => new { m.ReceiverId, m.SenderId, m.SentAt })
                .HasDatabaseName("IX_Messages_Conversation_Reverse")
                .IsDescending(false, false, true);

            // 4. Unread messages: Filtered index for getting unread count per sender (WHERE ReadAt IS NULL)
            //    Critical for badge counts and conversation list unread indicators
            builder.HasIndex(m => new { m.ReceiverId, m.SenderId })
                .HasDatabaseName("IX_Messages_Unread")
                .HasFilter("[ReadAt] IS NULL AND [IsDeleted] = 0");

            // 5. Message delivery status: For updating delivery/read timestamps by message ID
            //    Primary key handles this, but we add composite for batch operations
            builder.HasIndex(m => new { m.ReceiverId, m.DeliveredAt })
                .HasDatabaseName("IX_Messages_DeliveryStatus")
                .HasFilter("[DeliveredAt] IS NULL AND [IsDeleted] = 0");

            // ADD: Foreign key to Conversation (nullable during migration)
            builder.HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Restrict);

            // ADD: Index for conversation messages
            builder.HasIndex(m => new { m.ConversationId, m.SentAt })
                .HasDatabaseName("IX_Messages_ConversationId_SentAt")
                .IsDescending(false, true)
                .HasFilter("[IsDeleted] = 0");

            // New message fields defaults
            builder.Property(m => m.IsForwarded).HasDefaultValue(false);
            builder.Property(m => m.IsEdited).HasDefaultValue(false);

            // ReplyTo relationship (self reference)
            builder.HasOne(m => m.ReplyTo)
                .WithMany()
                .HasForeignKey(m => m.ReplyToId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasIndex(m => m.ReplyToId)
                .HasDatabaseName("IX_Messages_ReplyToId")
                .HasFilter("[ReplyToId] IS NOT NULL");
        }
    }
}
