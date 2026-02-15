#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PutZige.Domain.Entities;

namespace PutZige.Infrastructure.Data.Configurations;

public class ConversationConfiguration : BaseEntityConfiguration<Conversation>
{
    public override void Configure(EntityTypeBuilder<Conversation> builder)
    {
        base.Configure(builder);

        builder.ToTable("Conversations");

        builder.Property(c => c.IsGroup)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.LastActivity)
            .IsRequired();

        // Relationships
        builder.HasMany(c => c.Participants)
            .WithOne(p => p.Conversation)
            .HasForeignKey(p => p.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(c => c.LastActivity)
            .HasDatabaseName("IX_Conversations_LastActivity")
            .IsDescending();

        builder.HasIndex(c => new { c.IsDeleted, c.LastActivity })
            .HasDatabaseName("IX_Conversations_Active")
            .HasFilter("[IsDeleted] = 0");
    }
}
