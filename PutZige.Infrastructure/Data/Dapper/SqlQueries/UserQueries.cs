namespace PutZige.Infrastructure.Data.Dapper.SqlQueries;

public static class UserQueries
{
    public const string VERIFY_EMAIL_BY_TOKEN =
        """
        UPDATE Users
        SET IsEmailVerified          = 1,
            EmailVerificationToken       = NULL,
            EmailVerificationTokenExpiry = NULL,
            UpdatedAt                    = GETUTCDATE()
        WHERE EmailVerificationToken       = @Token
          AND IsDeleted                    = 0
          AND IsEmailVerified              = 0
          AND EmailVerificationTokenExpiry > GETUTCDATE();
        """;

    public const string GET_USER_PROFILE_BY_ID =
        """
        SELECT
            u.Id,
            u.Username,
            u.Email,
            u.DisplayName,
            u.Bio,
            u.ProfilePictureUrl,
            u.CreatedAt,
            u.LastLoginAt          AS LastSeenAt
        FROM Users u
        WHERE u.Id        = @Id
          AND u.IsDeleted = 0;
        """;

    // -------------------------------------------------------------------------
    // Simple non-paged search (used for quick lookup / mention autocomplete)
    // Hits IX_Users_Search (Username, DisplayName) filtered index.
    // Does NOT join UserSessions — caller gets no online status, intentional
    // since this is a lightweight lookup path.
    // -------------------------------------------------------------------------
    public const string SEARCH_USERS =
        """
        SELECT TOP (@Limit)
            u.Id,
            u.Username,
            u.DisplayName,
            u.Email,
            u.JobTitle,
            u.Bio,
            u.ProfilePictureUrl
        FROM Users u
        WHERE (u.Username    LIKE @Query
           OR  u.DisplayName LIKE @Query)
          AND u.Id        != @CurrentUserId
          AND u.IsDeleted  = 0
          AND u.IsActive   = 1
        ORDER BY
            CASE WHEN u.Username = @Query THEN 0 ELSE 1 END,
            u.Username;
        """;

    // -------------------------------------------------------------------------
    // Paged search — COUNT
    // Single-table scan on filtered index IX_Users_Active.
    // No joins needed for the count.
    // -------------------------------------------------------------------------
    public const string SEARCH_USERS_PAGED_COUNT =
        """
        SELECT COUNT_BIG(*)
        FROM Users u
        WHERE u.IsDeleted  = 0
          AND u.IsActive   = 1
          AND u.Id        != @ExcludeUserId
          AND (
                u.Username    LIKE @SearchTerm
             OR u.Email       LIKE @SearchTerm
             OR u.DisplayName LIKE @SearchTerm
              );
        """;

    // -------------------------------------------------------------------------
    // Paged search — DATA
    // UserSessions is 1:1 with Users (IX_Users on UserId is UNIQUE),
    // so a plain LEFT JOIN is safe — no fan-out, no CTE needed.
    // Hits IX_Users_Search for the WHERE, IX_Messages_Conversation* for order.
    // -------------------------------------------------------------------------
    public const string SEARCH_USERS_PAGED_DATA =
        """
        SELECT
            u.Id,
            u.Username,
            u.Email,
            u.DisplayName,
            u.ProfilePictureUrl,
            u.Bio,
            u.JobTitle,
            ISNULL(CAST(us.IsOnline AS TINYINT), 0) AS IsOnline,
            us.LastActiveAt                          AS LastSeen
        FROM Users u
        LEFT JOIN UserSessions us ON us.UserId = u.Id
        WHERE u.IsDeleted  = 0
          AND u.IsActive   = 1
          AND u.Id        != @ExcludeUserId
          AND (
                u.Username    LIKE @SearchTerm
             OR u.Email       LIKE @SearchTerm
             OR u.DisplayName LIKE @SearchTerm
              )
        ORDER BY
            CASE WHEN u.Username = @Query THEN 0 ELSE 1 END,
            CASE WHEN us.IsOnline = 1      THEN 0 ELSE 1 END,
            u.Username
        OFFSET     @Offset  ROWS
        FETCH NEXT @PageSize ROWS ONLY;
        """;

    // -------------------------------------------------------------------------
    // Recent contacts
    // Uses IX_Messages_Conversation + IX_Messages_Conversation_Reverse to
    // resolve both directions of the OR without a scan.
    // MAX(SentAt) OVER (PARTITION BY u.Id) is evaluated once per user inside
    // the subquery; the outer TOP + ORDER BY then sorts the already-windowed set.
    // DaysSince is kept as a parameter — caller decides the window.
    // -------------------------------------------------------------------------
    public const string RECENT_CONTACTS =
        """
        SELECT TOP (@Limit)
            sub.Id,
            sub.Username,
            sub.Email,
            sub.DisplayName,
            sub.ProfilePictureUrl,
            sub.Bio,
            sub.JobTitle,
            sub.IsOnline,
            sub.LastSeen
        FROM (
            SELECT DISTINCT
                u.Id,
                u.Username,
                u.Email,
                u.DisplayName,
                u.ProfilePictureUrl,
                u.Bio,
                u.JobTitle,
                ISNULL(CAST(us.IsOnline AS TINYINT), 0)       AS IsOnline,
                us.LastActiveAt                                AS LastSeen,
                MAX(m.SentAt) OVER (PARTITION BY u.Id)        AS LatestMessageAt
            FROM Messages m
            INNER JOIN Users u
                ON u.Id = CASE
                               WHEN m.SenderId   = @UserId THEN m.ReceiverId
                               WHEN m.ReceiverId = @UserId THEN m.SenderId
                           END
            LEFT JOIN UserSessions us ON us.UserId = u.Id
            WHERE m.IsDeleted  = 0
              AND u.IsDeleted  = 0
              AND u.IsActive   = 1
              AND (m.SenderId = @UserId OR m.ReceiverId = @UserId)
              AND m.SentAt   >= DATEADD(DAY, -@DaysSince, GETUTCDATE())
        ) AS sub
        ORDER BY sub.LatestMessageAt DESC;
        """;

    // -------------------------------------------------------------------------
    // Suggested users (friends-of-friends not yet contacted)
    //
    // MutualContacts  — everyone the current user has ever messaged.
    //                   Uses IX_Messages_Conversation + _Reverse.
    //
    // FriendsOfFriends — everyone those contacts have messaged, excluding:
    //                    • the current user himself
    //                    • anyone already in MutualContacts (direct contacts)
    //                    Replaces the expensive NOT EXISTS correlated subquery
    //                    with a set-based LEFT JOIN … WHERE … IS NULL, which
    //                    the optimiser can satisfy with the same covering indexes.
    // -------------------------------------------------------------------------
    public const string SUGGESTED_USERS =
        """
        WITH MutualContacts AS (
            SELECT DISTINCT
                CASE
                    WHEN m.SenderId   = @UserId THEN m.ReceiverId
                    ELSE                             m.SenderId
                END AS ContactId
            FROM Messages m
            WHERE (m.SenderId = @UserId OR m.ReceiverId = @UserId)
              AND m.IsDeleted = 0
        ),
        FriendsOfFriends AS (
            SELECT DISTINCT
                CASE
                    WHEN m2.SenderId   IN (SELECT ContactId FROM MutualContacts) THEN m2.ReceiverId
                    ELSE                                                               m2.SenderId
                END AS CandidateId
            FROM Messages m2
            WHERE (
                      m2.SenderId   IN (SELECT ContactId FROM MutualContacts)
                   OR m2.ReceiverId IN (SELECT ContactId FROM MutualContacts)
                  )
              AND m2.IsDeleted = 0
        )
        SELECT TOP (@Limit)
            u.Id,
            u.Username,
            u.Email,
            u.DisplayName,
            u.ProfilePictureUrl,
            u.Bio,
            u.JobTitle,
            ISNULL(CAST(us.IsOnline AS TINYINT), 0) AS IsOnline,
            us.LastActiveAt                          AS LastSeen
        FROM FriendsOfFriends fof
        INNER JOIN Users u
            ON u.Id = fof.CandidateId
        LEFT JOIN UserSessions us
            ON us.UserId = u.Id
        LEFT JOIN MutualContacts mc
            ON mc.ContactId = u.Id
        WHERE mc.ContactId  IS NULL        -- not already a direct contact
          AND u.Id          != @UserId     -- not the user himself
          AND u.IsDeleted    = 0
          AND u.IsActive     = 1
        ORDER BY
            CASE WHEN us.IsOnline = 1 THEN 0 ELSE 1 END,
            u.CreatedAt DESC;
        """;

    /// <summary>
    /// Update the user's session online status and last active timestamp.
    /// Parameterized: @UserId, @IsOnline, @LastActiveAt
    /// Also updates UpdatedAt = @LastActiveAt for consistency.
    /// </summary>
    public const string UPDATE_USER_SESSION_ONLINE_STATUS =
        """
        UPDATE UserSessions
        SET IsOnline     = @IsOnline,
            LastActiveAt = @LastActiveAt,
            UpdatedAt    = @LastActiveAt
        WHERE UserId = @UserId;
        """;
}