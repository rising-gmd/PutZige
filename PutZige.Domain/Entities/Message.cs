using System;

namespace PutZige.Domain.Entities
{
    public class Message : BaseEntity
    {
        public Guid SenderId { get; set; }
        public Guid ReceiverId { get; set; }

        public string MessageText { get; set; } = string.Empty;

        public DateTime SentAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ReadAt { get; set; }

    // Conversation support (nullable during migration)
    public Guid? ConversationId { get; set; }
    public Conversation? Conversation { get; set; }

        public User? Sender { get; set; }
        public User? Receiver { get; set; }
    
    // Attachments for this message
    public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
        
        // New fields
        public bool IsForwarded { get; set; } = false;
        public Guid? ReplyToId { get; set; }
        public Message? ReplyTo { get; set; }
        public bool IsEdited { get; set; } = false;
        public DateTime? EditedAt { get; set; }
    }
}
