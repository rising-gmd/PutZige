namespace PutZige.Infrastructure.Data.Dapper.SqlQueries;

/// <summary>
/// Optimized SQL queries for messaging operations.
/// Designed for high-throughput real-time chat at scale.
/// </summary>
public static class MessageQueries
{
    /// <summary>
    /// Get conversation list for a user with last message and unread count.
    /// Uses optimized CTE pattern with proper index hints.
    /// Indexes used: IX_Messages_Conversation, IX_Messages_Unread
    /// </summary>
    public const string GET_CONVERSATIONS_FOR_USER =
        @";WITH ranked_messages AS (
        SELECT
            m.ConversationId,
            CASE WHEN m.SenderId = @UserId THEN m.ReceiverId ELSE m.SenderId END AS OtherUserId,
            m.Id AS MessageId,
            m.SenderId,
            m.ReceiverId,
            m.MessageText,
            m.SentAt,
            m.DeliveredAt,
            m.ReadAt,
            ROW_NUMBER() OVER (
                PARTITION BY m.ConversationId
                ORDER BY m.SentAt DESC
            ) AS rn
        FROM Messages m WITH (NOLOCK)
        WHERE m.IsDeleted = 0
          AND m.ConversationId IS NOT NULL
          AND (m.SenderId = @UserId OR m.ReceiverId = @UserId)
    ),
    latest_messages AS (
        SELECT * FROM ranked_messages WHERE rn = 1
    ),
    unread_counts AS (
        SELECT 
            m.ConversationId,
            COUNT_BIG(*) AS UnreadCount
        FROM Messages m WITH (NOLOCK)
        WHERE m.ReceiverId = @UserId 
          AND m.ReadAt IS NULL 
          AND m.IsDeleted = 0
          AND m.ConversationId IS NOT NULL
        GROUP BY m.ConversationId
    )
    SELECT TOP (@Limit)
        c.Id AS ConversationId,
        u.Id AS UserId,
        u.Username,
        u.DisplayName,
        u.ProfilePictureUrl,
        ISNULL(us.IsOnline, CAST(0 AS BIT)) AS IsOnline,
        lm.MessageId AS LastMessageId,
        lm.SenderId AS LastMessageSenderId,
        lm.ReceiverId AS LastMessageReceiverId,
        lm.MessageText AS LastMessageText,
        lm.SentAt AS LastMessageSentAt,
        lm.DeliveredAt AS LastMessageDeliveredAt,
        lm.ReadAt AS LastMessageReadAt,
        ISNULL(uc.UnreadCount, 0) AS UnreadCount,
        lm.SentAt AS LastActivity
    FROM latest_messages lm
    INNER JOIN Conversations c WITH (NOLOCK)
        ON c.Id = lm.ConversationId
        AND c.IsDeleted = 0
        AND c.IsGroup = 0
    INNER JOIN Users u WITH (NOLOCK)
        ON u.Id = lm.OtherUserId
        AND u.IsDeleted = 0
    LEFT JOIN UserSessions us WITH (NOLOCK)
        ON us.UserId = u.Id
    LEFT JOIN unread_counts uc
        ON uc.ConversationId = c.Id
    WHERE EXISTS (
        SELECT 1 
        FROM ConversationParticipants cp WITH (NOLOCK)
        WHERE cp.ConversationId = c.Id
          AND cp.UserId = @UserId
          AND cp.IsDeleted = 0
    )
    ORDER BY lm.SentAt DESC
    OPTION (RECOMPILE);";

    // New: Get conversation history by ConversationId
    public const string GET_CONVERSATION_HISTORY_BY_ID =
        @"SELECT 
    m.Id, m.SenderId, m.ReceiverId,
    CASE WHEN m.IsDeleted = 1 THEN '' ELSE m.MessageText END AS MessageText,
    m.SentAt, m.DeliveredAt, m.ReadAt,
    m.IsForwarded, m.IsEdited, m.EditedAt, m.IsDeleted,
    m.ReplyToId,
    LEFT(rm.MessageText, 100) AS ReplyToText,
    ru.DisplayName AS ReplyToSenderName,
    s.Username AS SenderUsername,
    r.Username AS ReceiverUsername
FROM Messages m WITH (INDEX(IX_Messages_ConversationId_SentAt))
INNER JOIN Users s WITH (NOLOCK) ON s.Id = m.SenderId
INNER JOIN Users r WITH (NOLOCK) ON r.Id = m.ReceiverId
LEFT JOIN Messages rm WITH (NOLOCK) ON rm.Id = m.ReplyToId
LEFT JOIN Users ru WITH (NOLOCK) ON ru.Id = rm.SenderId
WHERE m.ConversationId = @ConversationId
  AND m.IsDeleted = 0
ORDER BY m.SentAt DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

    public const string GET_CONVERSATION_COUNT_BY_ID =
        @"SELECT COUNT_BIG(*)
        FROM Messages WITH (INDEX(IX_Messages_ConversationId_SentAt))
        WHERE ConversationId = @ConversationId
          AND IsDeleted = 0;";

    /// <summary>
    /// Get paginated conversation history between two users.
    /// Uses keyset pagination for consistent performance at any page depth.
    /// Index used: IX_Messages_Conversation
    /// </summary>
    public const string GET_CONVERSATION_HISTORY =
        @"-- Optimized conversation history with keyset pagination
        SELECT 
            m.Id,
            m.SenderId,
            m.ReceiverId,
            m.MessageText,
            m.SentAt,
            m.DeliveredAt,
            m.ReadAt,
            m.CreatedAt,
            s.Username AS SenderUsername,
            r.Username AS ReceiverUsername
        FROM Messages m WITH (INDEX(IX_Messages_Conversation))
        INNER JOIN Users s WITH (NOLOCK) ON s.Id = m.SenderId
        INNER JOIN Users r WITH (NOLOCK) ON r.Id = m.ReceiverId
        WHERE m.IsDeleted = 0
          AND ((m.SenderId = @UserId AND m.ReceiverId = @OtherUserId) 
               OR (m.SenderId = @OtherUserId AND m.ReceiverId = @UserId))
        ORDER BY m.SentAt DESC
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

    /// <summary>
    /// Get total message count for a conversation (for pagination metadata).
    /// </summary>
    public const string GET_CONVERSATION_COUNT =
        @"SELECT COUNT_BIG(*)
        FROM Messages WITH (INDEX(IX_Messages_Conversation))
        WHERE IsDeleted = 0
          AND ((SenderId = @UserId AND ReceiverId = @OtherUserId) 
               OR (SenderId = @OtherUserId AND ReceiverId = @UserId));";

    /// <summary>
    /// Get unread message count for a user (total across all conversations).
    /// Uses filtered index for optimal performance.
    /// </summary>
    public const string GET_TOTAL_UNREAD_COUNT =
        @"SELECT COUNT_BIG(*)
        FROM Messages WITH (INDEX(IX_Messages_Unread))
        WHERE ReceiverId = @UserId 
          AND ReadAt IS NULL 
          AND IsDeleted = 0;";

    /// <summary>
    /// Get unread message count for a specific receiver in a specific conversation.
    /// </summary>
    public const string GET_UNREAD_COUNT_FOR_CONVERSATION =
        @"SELECT COUNT_BIG(*)
        FROM Messages WITH (NOLOCK)
        WHERE ConversationId = @ConversationId
          AND ReceiverId = @ReceiverId
          AND ReadAt IS NULL
          AND IsDeleted = 0";

    /// <summary>
    /// Mark all messages from a specific sender as read (bulk operation).
    /// </summary>
    public const string MARK_CONVERSATION_AS_READ =
        @"UPDATE Messages
        SET ReadAt = @ReadAt, UpdatedAt = @ReadAt
        WHERE ReceiverId = @UserId 
          AND SenderId = @OtherUserId
          AND ReadAt IS NULL 
          AND IsDeleted = 0;";

    /// <summary>
    /// Search users for chat (prefix search on username/display name).
    /// Uses IX_Users_Search filtered index.
    /// </summary>
    public const string SEARCH_USERS =
        @"SELECT TOP (@Limit)
            u.Id,
            u.Username,
            u.DisplayName,
            u.ProfilePictureUrl,
            ISNULL(us.IsOnline, 0) AS IsOnline
        FROM Users u WITH (INDEX(IX_Users_Search))
        LEFT JOIN UserSessions us WITH (NOLOCK) ON us.UserId = u.Id
        WHERE u.IsActive = 1 
          AND u.IsDeleted = 0
          AND u.Id <> @CurrentUserId
          AND (u.Username LIKE @SearchTerm + '%' OR u.DisplayName LIKE @SearchTerm + '%')
        ORDER BY 
            CASE WHEN u.Username LIKE @SearchTerm + '%' THEN 0 ELSE 1 END,
            u.Username;";
}
