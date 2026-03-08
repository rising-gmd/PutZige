using System;

namespace PutZige.Domain.Entities
{
    public class MessageStar
    {
        public Guid UserId { get; set; }
        public Guid MessageId { get; set; }
        public DateTime StarredAt { get; set; }

        // Navigation
        public User User { get; set; } = null!;
        public Message Message { get; set; } = null!;
    }
}
