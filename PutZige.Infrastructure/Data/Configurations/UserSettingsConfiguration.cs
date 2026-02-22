using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PutZige.Domain.Entities;
using PutZige.Infrastructure.Data.Configurations;
using System.Text.Json;

public class UserSettingsConfiguration : BaseEntityConfiguration<UserSettings>
{
    public override void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        base.Configure(builder);

        builder.ToTable("UserSettings");

        builder.Property(s => s.ShowOnlineStatus).HasDefaultValue(true);
        builder.Property(s => s.AllowFriendRequests).HasDefaultValue(true);
        builder.Property(s => s.EmailNotifications).HasDefaultValue(true);
        builder.Property(s => s.PushNotifications).HasDefaultValue(true);

        builder.Property(s => s.Preferences)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<UserPreferences>(v, JsonSerializerOptions.Default) ?? new UserPreferences()
            )
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue(new UserPreferences());

        builder.HasIndex(s => s.UserId).IsUnique();

        builder.HasOne(s => s.User)
            .WithOne(u => u.Settings)
            .HasForeignKey<UserSettings>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}