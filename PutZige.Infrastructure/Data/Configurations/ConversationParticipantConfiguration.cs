#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PutZige.Domain.Entities;

namespace PutZige.Infrastructure.Data.Configurations;

public class ConversationParticipantConfiguration : BaseEntityConfiguration<ConversationParticipant>
{
    public override void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        base.Configure(builder);

        builder.ToTable("ConversationParticipants");

        builder.Property(p => p.ConversationId).IsRequired();
        builder.Property(p => p.UserId).IsRequired();
        builder.Property(p => p.IsPinned).HasDefaultValue(false);
        builder.Property(p => p.IsMuted).HasDefaultValue(false);

        // Relationships
        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(p => new { p.UserId, p.ConversationId })
            .HasDatabaseName("IX_ConversationParticipants_User")
            .IsUnique();

        builder.HasIndex(p => new { p.ConversationId, p.UserId })
            .HasDatabaseName("IX_ConversationParticipants_Conversation");

        builder.HasIndex(p => new { p.UserId, p.IsPinned })
            .HasDatabaseName("IX_ConversationParticipants_UserPinned")
            .HasFilter("[IsPinned] = 1 AND [IsDeleted] = 0");
    }
}
