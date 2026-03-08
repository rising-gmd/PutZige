using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PutZige.Domain.Entities;

namespace PutZige.Infrastructure.Data.Configurations
{
    public class MessageStarConfiguration : IEntityTypeConfiguration<MessageStar>
    {
        public void Configure(EntityTypeBuilder<MessageStar> builder)
        {
            builder.ToTable("MessageStars");
            builder.HasKey(s => new { s.UserId, s.MessageId });
            builder.Property(s => s.StarredAt).IsRequired();

            builder.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(s => s.Message)
                .WithMany()
                .HasForeignKey(s => s.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(s => s.UserId)
                .HasDatabaseName("IX_MessageStars_UserId");
        }
    }
}