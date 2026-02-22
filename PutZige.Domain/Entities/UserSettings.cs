using System;

namespace PutZige.Domain.Entities
{
    public class UserSettings : BaseEntity
    {
        public Guid UserId { get; set; }

        public bool ShowOnlineStatus { get; set; } = true;
        public bool AllowFriendRequests { get; set; } = true;
        public bool EmailNotifications { get; set; } = true;
        public bool PushNotifications { get; set; } = true;

        public UserPreferences Preferences { get; set; } = new();

        public User? User { get; set; }
    }
}
