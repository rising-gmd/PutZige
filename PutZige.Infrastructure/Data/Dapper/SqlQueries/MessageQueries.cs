namespace PutZige.Infrastructure.Data.Dapper.SqlQueries;

public static class MessageQueries
{
    public const string GET_CONVERSATIONS_FOR_USER =
        @"-- Get latest message and counts per conversation
        WITH user_messages AS (
            SELECT
                CASE WHEN SenderId = @UserId THEN ReceiverId ELSE SenderId END AS OtherUserId,
                Id AS MessageId,
                SenderId,
                ReceiverId,
                MessageText,
                SentAt,
                DeliveredAt,
                ReadAt
            FROM Messages
            WHERE (SenderId = @UserId OR ReceiverId = @UserId) AND IsDeleted = 0
        ), last_messages AS (
            SELECT um.*, ROW_NUMBER() OVER (PARTITION BY OtherUserId ORDER BY SentAt DESC) AS rn
            FROM user_messages um
        ), last_per_conversation AS (
            SELECT * FROM last_messages WHERE rn = 1
        ), unread_counts AS (
            SELECT
                SenderId AS FromUser,
                COUNT(*) AS UnreadCount
            FROM Messages
            WHERE ReceiverId = @UserId AND ReadAt IS NULL AND IsDeleted = 0
            GROUP BY SenderId
        )
        SELECT
            u.Id AS UserId,
            u.Username,
            u.DisplayName,
            u.ProfilePictureUrl,
            ISNULL(us.IsOnline, 0) AS IsOnline,
            l.MessageId AS LastMessageId,
            l.SenderId AS LastMessageSenderId,
            l.ReceiverId AS LastMessageReceiverId,
            l.MessageText AS LastMessageText,
            l.SentAt AS LastMessageSentAt,
            l.DeliveredAt AS LastMessageDeliveredAt,
            l.ReadAt AS LastMessageReadAt,
            ISNULL(uc.UnreadCount, 0) AS UnreadCount,
            l.SentAt AS LastActivity
        FROM last_per_conversation l
        JOIN Users u ON u.Id = l.OtherUserId
        LEFT JOIN UserSessions us ON us.UserId = u.Id
        LEFT JOIN unread_counts uc ON uc.FromUser = u.Id
        ORDER BY l.SentAt DESC;";
}
