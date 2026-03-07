using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PutZige.Domain.Entities;
using PutZige.Application.Common.Constants;

namespace PutZige.Infrastructure.Data.Configurations
{
    public class MessageAttachmentConfiguration : BaseEntityConfiguration<MessageAttachment>
    {
        public override void Configure(EntityTypeBuilder<MessageAttachment> builder)
        {
            base.Configure(builder);

            builder.ToTable("MessageAttachments");

            builder.Property(a => a.MessageId).IsRequired();

            builder.Property(a => a.Type).IsRequired().HasMaxLength(AppConstants.Messaging.MaxAttachmentTypeLength);
            builder.Property(a => a.Url).IsRequired().HasMaxLength(AppConstants.Validation.MaxUrlLength);
            builder.Property(a => a.ThumbnailUrl).HasMaxLength(AppConstants.Validation.MaxUrlLength);
            builder.Property(a => a.FileName).HasMaxLength(AppConstants.Messaging.MaxAttachmentFileNameLength);
            builder.Property(a => a.MimeType).HasMaxLength(AppConstants.Messaging.MaxAttachmentMimeTypeLength);
            builder.Property(a => a.DownloadUrl).HasMaxLength(AppConstants.Validation.MaxUrlLength);
            builder.Property(a => a.Caption).HasMaxLength(AppConstants.Messaging.MaxAttachmentCaptionLength);

            // Relationship to Message
            builder.HasOne(a => a.Message)
                .WithMany(m => m.Attachments)
                .HasForeignKey(a => a.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for MessageId
            builder.HasIndex(a => a.MessageId)
                .HasDatabaseName("IX_MessageAttachments_MessageId");
        }
    }
}
